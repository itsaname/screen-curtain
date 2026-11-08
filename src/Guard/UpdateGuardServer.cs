using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace ScreenCurtain
{
    internal static class UpdateGuardServer
    {
        private static readonly object RestoreSync = new object();
        private static readonly object PolicySync = new object();
        private static readonly object WriteSync = new object();
        private static bool _restored;
        private static volatile bool _paused;
        private static int _parentExitReported;
        private static int _healthFailures;
        private static PolicySnapshot _snapshot;
        private static StreamReader _reader;
        private static StreamWriter _writer;
        private static Timer _healthTimer;

        public static void Run(string[] args)
        {
            if (args != null && Array.IndexOf(args, "--test-memory-policy") >= 0)
            {
                // Development-only path used by --test-guard-protocol. Normal
                // program launches never pass this argument.
                PolicyRegistry.Store = new MemoryPolicyStore();
                Log.Warn("update-guard 使用内存策略存储（测试模式）");
            }

            int parentPid = PipeProtocol.IntArgument(args, "--parent", 0);
            string pipeName = PipeProtocol.Argument(args, "--pipe");
            string token = PipeProtocol.Argument(args, "--token");
            bool startPaused = args != null && Array.IndexOf(args, "--start-paused") >= 0;

            if (parentPid <= 0 || string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(token))
            {
                Log.Error("update-guard 参数不完整");
                return;
            }

            IntPtr parentHandle = Native.OpenProcess(
                Native.PROCESS_SYNCHRONIZE | Native.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                parentPid);
            if (parentHandle == IntPtr.Zero)
            {
                // The pipe itself is also a parent lifetime signal. Keep working
                // even when an over-the-shoulder admin cannot open the parent
                // process handle.
                Log.Warn("update-guard 无法打开父进程句柄，将仅依赖管道关闭检测父进程退出，Win32={0}",
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            }

            NamedPipeClientStream pipe = null;
            Thread parentWatch = null;
            try
            {
                pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(10000);
                _reader = PipeProtocol.NewReader(pipe);
                _writer = PipeProtocol.NewWriter(pipe);

                // The helper speaks first. The client verifies the one-time token
                // and then acknowledges with OK. Reading here as well would
                // deadlock both sides before any policy is applied.
                WriteLine(PipeProtocol.Hello + " " + token);
                string ack = _reader.ReadLine();
                if (ack == null ||
                    (!string.Equals(ack, PipeProtocol.Ok, StringComparison.OrdinalIgnoreCase) &&
                     !ack.StartsWith(PipeProtocol.Ok + " ", StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Error("update-guard 握手被拒绝：{0}", ack);
                    return;
                }

                if (parentHandle != IntPtr.Zero)
                {
                    parentWatch = new Thread(delegate()
                    {
                        uint wait = Native.WaitForSingleObject(parentHandle, 0xFFFFFFFF);
                        if (wait == Native.WAIT_OBJECT_0)
                        {
                            RestoreAndExit("父进程已退出（" + ParentExitDetail(parentHandle, parentPid) + "）", false);
                        }
                    });
                    parentWatch.IsBackground = true;
                    parentWatch.Name = "ScreenCurtain.UpdateGuardParentWatch";
                    parentWatch.Start();
                }

                try
                {
                    lock (PolicySync)
                    {
                        PolicyManager.RecoverOrphaned();
                        _snapshot = PolicyManager.Capture(parentPid);
                        _snapshot.Save();
                        if (startPaused)
                        {
                            _paused = true;
                        }
                        else
                        {
                            PolicyManager.ApplyTemporary();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("启用更新保护失败", ex);
                    if (_snapshot != null)
                    {
                        try
                        {
                            lock (PolicySync)
                            {
                                PolicyManager.Restore(_snapshot);
                            }
                        }
                        catch (Exception rex) { Log.Exception("回滚策略失败", rex); }
                    }
                    WriteLine(PipeProtocol.Error + " " + OneLine(ex.Message));
                    return;
                }

                WriteLine(startPaused
                    ? PipeProtocol.Paused + " 更新保护已就绪（等待熄屏）"
                    : PipeProtocol.Active + " 更新保护已启用（临时策略）");
                Log.Info(startPaused
                    ? "update-guard 已就绪（已暂停），父进程 {0}"
                    : "update-guard 已启用，父进程 {0}", parentPid);

                _healthTimer = new Timer(HealthTick, null, 60000, 60000);

                string line;
                while (true)
                {
                    try
                    {
                        line = _reader.ReadLine();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception("读取 guard 管道失败", ex);
                        line = null;
                    }

                    if (line == null)
                    {
                        RestoreAndExit("管道关闭（" + ParentExitDetail(parentHandle, parentPid) + "）", false);
                        return;
                    }

                    if (line.StartsWith(PipeProtocol.Restore, StringComparison.OrdinalIgnoreCase))
                    {
                        RestoreAndExit("收到恢复命令", true);
                        return;
                    }

                    if (line.StartsWith(PipeProtocol.Pause, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            lock (PolicySync)
                            {
                                if (!_paused)
                                {
                                    PolicyManager.Restore(_snapshot);
                                    _paused = true;
                                    _healthFailures = 0;
                                }
                            }
                            WriteLine(PipeProtocol.Paused + " 更新保护已暂停（仅熄屏期间启用）");
                            Log.Info("update-guard 已暂停，策略已恢复");
                        }
                        catch (Exception ex)
                        {
                            Log.Exception("暂停更新保护失败", ex);
                            WriteLine(PipeProtocol.Error + " 暂停失败：" + OneLine(ex.Message));
                        }
                        continue;
                    }

                    if (line.StartsWith(PipeProtocol.Resume, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            lock (PolicySync)
                            {
                                if (_paused)
                                {
                                    PolicyManager.ApplyTemporary();
                                    _paused = false;
                                    _healthFailures = 0;
                                }
                            }
                            WriteLine(PipeProtocol.Active + " 更新保护已恢复（仅熄屏期间启用）");
                            Log.Info("update-guard 已恢复临时策略");
                        }
                        catch (Exception ex)
                        {
                            Log.Exception("恢复更新保护失败", ex);
                            WriteLine(PipeProtocol.Error + " 恢复失败：" + OneLine(ex.Message));
                        }
                        continue;
                    }

                    if (line.StartsWith("PING", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteLine("PONG");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("update-guard 异常", ex);
                TryRestore();
            }
            finally
            {
                try
                {
                    if (_healthTimer != null) _healthTimer.Dispose();
                }
                catch { }
                try { if (_writer != null) _writer.Dispose(); } catch { }
                try { if (_reader != null) _reader.Dispose(); } catch { }
                try { if (pipe != null) pipe.Dispose(); } catch { }
                if (parentHandle != IntPtr.Zero)
                {
                    try { Native.CloseHandle(parentHandle); } catch { }
                }
            }
        }

        private static void HealthTick(object state)
        {
            try
            {
                lock (PolicySync)
                {
                    if (_restored || _paused)
                    {
                        return;
                    }

                    string error;
                    if (PolicyManager.CheckHealth(out error))
                    {
                        _healthFailures = 0;
                        Log.Info("策略健康检查通过");
                    }
                    else
                    {
                        _healthFailures++;
                        Log.Warn("策略健康检查失败（{0}/3）：{1}", _healthFailures, error);
                        WriteLine(PipeProtocol.Degraded + " 策略被系统或组策略改写：" + OneLine(error));
                        if (_healthFailures >= 3 && _healthTimer != null)
                        {
                            _healthTimer.Change(Timeout.Infinite, Timeout.Infinite);
                            Log.Warn("策略连续失败 3 次，停止自动重试，等待用户修复");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("策略健康检查异常", ex);
            }
        }

        private static string ParentExitDetail(IntPtr parentHandle, int parentPid)
        {
            if (Interlocked.Exchange(ref _parentExitReported, 1) != 0)
            {
                return "exit already reported";
            }

            uint code = 0;
            bool hasCode = parentHandle != IntPtr.Zero &&
                Native.GetExitCodeProcess(parentHandle, out code);
            string detail = string.Format(
                "Parent process exited.\r\nPID: {0}\r\nExit code: {1}\r\nExit code (hex): 0x{2:X8}\r\n",
                parentPid,
                hasCode ? code.ToString() : "unknown",
                hasCode ? code : 0);
            Log.Warn("update-guard 检测到父进程退出：PID={0}, ExitCode={1} (0x{2:X8})",
                parentPid, hasCode ? code.ToString() : "unknown", hasCode ? code : 0);
            try { CrashLog.Write("parent-exit", detail); } catch { }
            return hasCode ? "ExitCode=0x" + code.ToString("X8") : "ExitCode=unknown";
        }

        private static void RestoreAndExit(string reason, bool notify)
        {
            lock (RestoreSync)
            {
                if (_restored)
                {
                    return;
                }
                _restored = true;
            }

            Log.Info("update-guard 恢复：{0}", reason);
            try
            {
                if (_healthTimer != null) _healthTimer.Dispose();
            }
            catch { }

            bool ok = false;
            try
            {
                lock (PolicySync)
                {
                    if (_snapshot != null)
                    {
                        PolicyManager.Restore(_snapshot);
                        ok = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("update-guard 恢复策略失败", ex);
                WriteLine(PipeProtocol.Error + " 恢复更新设置失败：" + OneLine(ex.Message));
            }

            if (notify)
            {
                if (ok)
                {
                    WriteLine(PipeProtocol.Restored);
                }
            }

            try
            {
                if (_writer != null) _writer.Flush();
            }
            catch { }
            Environment.Exit(ok ? 0 : 2);
        }

        private static void TryRestore()
        {
            try
            {
                if (_snapshot != null)
                {
                    PolicyManager.Restore(_snapshot);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("异常路径恢复策略失败", ex);
            }
        }

        private static void WriteLine(string line)
        {
            lock (WriteSync)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.WriteLine(line);
                        _writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("写入 guard 管道失败", ex);
                }
            }
        }

        private static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "未知错误";
            return text.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
