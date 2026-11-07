using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class UpdateGuardClient : IDisposable
    {
        private readonly object _sync = new object();
        private readonly ManualResetEvent _doneEvent = new ManualResetEvent(false);
        private readonly object _writeSync = new object();

        private GuardState _state = GuardState.Off;
        private string _message = "更新重启保护未启动";
        private Thread _worker;
        private NamedPipeServerStream _pipe;
        private StreamReader _reader;
        private StreamWriter _writer;
        private string _token;
        private string _pipeName;
        private volatile bool _stopping;
        private volatile bool _wasActive;
        private volatile bool _restoreAcknowledged;
        private volatile bool _restoreFailed;
        private bool _disposed;        public event EventHandler<GuardStatusEventArgs> StatusChanged;

        public GuardState State
        {
            get { lock (_sync) { return _state; } }
        }

        public string Message
        {
            get { lock (_sync) { return _message; } }
        }

        public bool Start()
        {
            if (State == GuardState.Degraded)
            {
                // Repair path: first close any stale helper/pipe so an old
                // worker cannot overwrite the new state.
                try { Stop(); } catch (Exception ex) { Log.Exception("重启更新保护前清理失败", ex); }
            }

            lock (_sync)
            {
                if (_disposed) return false;
                if (_state == GuardState.Starting || _state == GuardState.Active || _state == GuardState.Stopping)
                {
                    return false;
                }

                _stopping = false;
                _wasActive = false;
                _restoreAcknowledged = false;
                _restoreFailed = false;
                _doneEvent.Reset();
                _state = GuardState.Starting;
                _message = "正在请求管理员权限并启动更新保护...";
                _worker = new Thread(Worker);
                _worker.IsBackground = true;
                _worker.Name = "ScreenCurtain.UpdateGuardClient";
                _worker.Start();
            }

            RaiseStatus(GuardState.Starting, "正在请求管理员权限并启动更新保护...");
            return true;
        }

        public void Stop()
        {
            bool shouldWait = false;
            lock (_sync)
            {
                if (_disposed && _state == GuardState.Off) return;
                if (_state == GuardState.Off) return;

                _stopping = true;
                _state = GuardState.Stopping;
                _message = "正在恢复 Windows Update 原始设置...";
                shouldWait = true;            }

            RaiseStatus(GuardState.Stopping, "正在恢复 Windows Update 原始设置...");
            WriteLine(PipeProtocol.Restore);

            if (shouldWait)
            {
                _doneEvent.WaitOne(6000);
            }

            lock (_sync)
            {
                ClosePipeNoLock();
            }

            try
            {
                if (_worker != null && _worker.IsAlive)
                {
                    _worker.Join(1000);
                }
            }
            catch
            {
            }

            bool failed = false;
            lock (_sync)
            {
                failed = _restoreFailed || (_wasActive && !_restoreAcknowledged);
                _state = failed ? GuardState.Degraded : GuardState.Off;
                _message = failed ? "更新设置恢复失败，请用“修复更新设置”重试" : "更新重启保护已关闭";
                _stopping = false;
                _worker = null;
                try { _doneEvent.Set(); } catch { }
            }

            if (failed)
            {
                RaiseStatus(GuardState.Degraded, "更新设置恢复失败，请用“修复更新设置”重试");
            }
            else
            {
                RaiseStatus(GuardState.Off, "更新重启保护已关闭");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                Log.Exception("释放更新保护客户端失败", ex);
            }
            _disposed = true;
            try
            {
                // Do not dispose _doneEvent while an escaped UAC worker could
                // still reach its finally block.
                if (_worker == null || !_worker.IsAlive)
                {
                    _doneEvent.Dispose();
                }
            }
            catch
            {
            }
        }

        private void Worker()
        {
            NamedPipeServerStream server = null;
            try
            {
                string pipeName = "ScreenCurtain.Guard." + Guid.NewGuid().ToString("N");
                string token = Guid.NewGuid().ToString("N");
                lock (_sync)
                {
                    _pipeName = pipeName;
                    _token = token;
                }

                server = PipeProtocol.CreateServer(pipeName);
                lock (_sync)
                {
                    if (_stopping)
                    {
                        ClosePipeNoLock();
                        return;
                    }
                    _pipe = server;
                }

                Task waitTask = server.WaitForConnectionAsync();
                string launchError;
                if (!LaunchHelper(pipeName, token, out launchError))
                {
                    SetState(GuardState.Degraded, launchError);
                    return;
                }

                bool connected;
                try
                {
                    connected = waitTask.Wait(15000);
                }
                catch (AggregateException)
                {
                    connected = false;
                }

                if (!connected)
                {
                    SetState(GuardState.Degraded, "更新保护辅助进程未在 15 秒内连接（可能被安全软件阻止或 UAC 未完成）");
                    return;
                }

                lock (_sync)
                {
                    if (_stopping)
                    {
                        ClosePipeNoLock();
                        return;
                    }
                    _reader = PipeProtocol.NewReader(_pipe);
                    _writer = PipeProtocol.NewWriter(_pipe);
                }

                string hello = _reader.ReadLine();
                if (hello == null || !hello.StartsWith(PipeProtocol.Hello + " ", StringComparison.Ordinal))
                {
                    SetState(GuardState.Degraded, "辅助进程握手失败");
                    return;
                }

                string receivedToken = hello.Substring(PipeProtocol.Hello.Length + 1);
                if (!string.Equals(receivedToken, token, StringComparison.Ordinal))
                {
                    WriteLine(PipeProtocol.Deny);
                    SetState(GuardState.Degraded, "辅助进程令牌校验失败");
                    return;
                }

                WriteLine(PipeProtocol.Ok);

                string status = _reader.ReadLine();
                if (status == null)
                {
                    SetState(GuardState.Degraded, "辅助进程在应用策略前断开");
                    return;
                }

                if (status.StartsWith(PipeProtocol.Active, StringComparison.OrdinalIgnoreCase))
                {
                    string message = status.Length > PipeProtocol.Active.Length
                        ? status.Substring(PipeProtocol.Active.Length).Trim()
                        : "更新保护已启用";
                    _wasActive = true;
                    SetState(GuardState.Active, message);
                }
                else if (status.StartsWith(PipeProtocol.Error, StringComparison.OrdinalIgnoreCase))
                {
                    string message = status.Length > PipeProtocol.Error.Length
                        ? status.Substring(PipeProtocol.Error.Length).Trim()
                        : "更新保护启用失败";
                    SetState(GuardState.Degraded, message);
                    return;
                }
                else
                {
                    SetState(GuardState.Degraded, "辅助进程返回未知状态: " + OneLine(status));
                    return;
                }

                while (true)
                {
                    string line;
                    try
                    {
                        line = _reader.ReadLine();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception("读取更新保护状态失败", ex);
                        line = null;
                    }

                    if (line == null)
                    {
                        if (_stopping && _wasActive && !_restoreAcknowledged)
                        {
                            _restoreFailed = true;
                        }
                        else if (!_stopping)
                        {
                            SetState(GuardState.Degraded, "更新保护辅助进程连接已断开");
                        }
                        break;
                    }

                    if (line.StartsWith(PipeProtocol.Degraded, StringComparison.OrdinalIgnoreCase))
                    {
                        string message = line.Length > PipeProtocol.Degraded.Length
                            ? line.Substring(PipeProtocol.Degraded.Length).Trim()
                            : "更新保护降级";
                        SetState(GuardState.Degraded, message);
                    }
                    else if (line.StartsWith(PipeProtocol.Error, StringComparison.OrdinalIgnoreCase))
                    {
                        string message = line.Length > PipeProtocol.Error.Length
                            ? line.Substring(PipeProtocol.Error.Length).Trim()
                            : "更新保护错误";
                        if (_stopping)
                        {
                            _restoreFailed = true;
                        }
                        SetState(GuardState.Degraded, message);
                    }
                    else if (line.StartsWith(PipeProtocol.Restored, StringComparison.OrdinalIgnoreCase))
                    {
                        _restoreAcknowledged = true;
                        _doneEvent.Set();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("更新保护客户端异常", ex);
                if (!_stopping)
                {
                    SetState(GuardState.Degraded, "更新保护异常：" + OneLine(ex.Message));
                }
            }
            finally
            {
                lock (_sync)
                {
                    ClosePipeNoLock();
                }
                try { _doneEvent.Set(); } catch { }
            }
        }

        private bool LaunchHelper(string pipeName, string token, out string error)
        {
            error = null;
            IntPtr helperProcess = IntPtr.Zero;
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                string exe = Application.ExecutablePath;
                string args = "--update-guard --parent " + pid +
                              " --pipe " + PipeProtocol.Quote(pipeName) +
                              " --token " + PipeProtocol.Quote(token);

                SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO));
                info.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC;
                info.lpVerb = "runas";
                info.lpFile = exe;
                info.lpParameters = args;
                info.nShow = 0;

                bool ok = ShellExecuteEx(ref info);
                if (!ok)
                {
                    int lastError = Marshal.GetLastWin32Error();
                    if (lastError == Native.ERROR_CANCELLED)
                    {
                        error = "管理员权限请求已取消，未启用更新重启保护";
                    }
                    else
                    {
                        error = "无法启动管理员权限辅助进程（Win32=" + lastError + "）";
                    }
                    return false;
                }

                helperProcess = info.hProcess;
                if (helperProcess == IntPtr.Zero)
                {
                    error = "提权辅助进程未返回进程句柄";
                    return false;
                }

                // The helper owns the guard lifecycle and watches this process.
                // Closing our copy of the process handle does not terminate it.
                return true;
            }
            catch (Exception ex)
            {
                error = "启动更新保护辅助进程异常：" + ex.Message;
                Log.Exception("启动更新保护辅助进程异常", ex);
                return false;
            }
            finally
            {
                if (helperProcess != IntPtr.Zero)
                {
                    try { Native.CloseHandle(helperProcess); } catch { }
                }
            }
        }

        private void SetState(GuardState state, string message)
        {
            bool changed;
            lock (_sync)
            {
                if (Thread.CurrentThread != _worker)
                {
                    return;
                }
                changed = _state != state || !string.Equals(_message, message, StringComparison.Ordinal);
                _state = state;
                _message = message;
            }
            if (changed)
            {
                RaiseStatus(state, message);
            }
        }

        private void RaiseStatus(GuardState state, string message)
        {
            EventHandler<GuardStatusEventArgs> handler = StatusChanged;
            if (handler != null)
            {
                try { handler(this, new GuardStatusEventArgs(state, message)); } catch (Exception ex) { Log.Exception("更新保护状态回调异常", ex); }
            }
        }

        private bool WriteLine(string line)
        {
            lock (_writeSync)
            {
                try
                {
                    StreamWriter writer;
                    lock (_sync)
                    {
                        writer = _writer;
                    }
                    if (writer == null)
                    {
                        return false;
                    }
                    writer.WriteLine(line);
                    writer.Flush();
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Exception("写入更新保护管道失败", ex);
                    return false;
                }
            }
        }

        private void ClosePipeNoLock()
        {
            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_reader != null) _reader.Dispose(); } catch { }
            try { if (_pipe != null) _pipe.Dispose(); } catch { }
            _writer = null;
            _reader = null;
            _pipe = null;
        }

        private static string OneLine(string message)
        {
            if (string.IsNullOrEmpty(message)) return "未知错误";
            return message.Replace("\r", " ").Replace("\n", " ");
        }

        private const uint SEE_MASK_NOCLOSEPROCESS = 0x00000040;
        private const uint SEE_MASK_NOASYNC = 0x00000100;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public string lpVerb;
            public string lpFile;
            public string lpParameters;
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);
    }
}
