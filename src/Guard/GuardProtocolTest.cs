using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class GuardProtocolTest
    {
        public static void Run()
        {
            StringBuilder result = new StringBuilder();
            bool passed = false;
            NamedPipeServerStream server = null;
            StreamReader reader = null;
            StreamWriter writer = null;
            Process helper = null;
            try
            {
                result.AppendLine("ScreenCurtain update-guard protocol test");
                result.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                string pipeName = "ScreenCurtain.TestGuard." + Guid.NewGuid().ToString("N");
                string token = Guid.NewGuid().ToString("N");
                int parentPid = Process.GetCurrentProcess().Id;
                server = PipeProtocol.CreateServer(pipeName);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = "--update-guard --test-memory-policy --parent " + parentPid +
                                " --pipe " + PipeProtocol.Quote(pipeName) +
                                " --token " + PipeProtocol.Quote(token);
                psi.UseShellExecute = false;
                psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                psi.EnvironmentVariables["SCREENCURTAIN_LOG_DIR"] = Log.DirectoryPath;
                helper = Process.Start(psi);
                if (helper == null)
                {
                    throw new InvalidOperationException("辅助进程启动失败");
                }

                bool connected;
                try
                {
                    connected = server.WaitForConnectionAsync().Wait(10000);
                }
                catch (AggregateException ex)
                {
                    connected = false;
                    result.AppendLine("Connect exception: " + ex.GetBaseException().Message);
                }
                result.AppendLine("Connected: " + connected);
                if (!connected)
                {
                    throw new InvalidOperationException("辅助进程未在 10 秒内连接命名管道");
                }

                reader = PipeProtocol.NewReader(server);
                writer = PipeProtocol.NewWriter(server);

                string hello = reader.ReadLine();
                result.AppendLine("HELLO: " + OneLine(hello));
                if (hello == null || !hello.StartsWith(PipeProtocol.Hello + " ", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("HELLO 格式错误");
                }
                if (!string.Equals(hello.Substring(PipeProtocol.Hello.Length + 1), token, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("token 不匹配");
                }
                writer.WriteLine(PipeProtocol.Ok);

                string active = reader.ReadLine();
                result.AppendLine("STATUS: " + OneLine(active));
                if (active == null || !active.StartsWith(PipeProtocol.Active, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("辅助进程未进入 ACTIVE");
                }

                writer.WriteLine(PipeProtocol.Restore);
                string restored = reader.ReadLine();
                result.AppendLine("RESTORE: " + OneLine(restored));
                if (restored == null || !restored.StartsWith(PipeProtocol.Restored, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("辅助进程未确认恢复");
                }

                bool exited = helper.WaitForExit(8000);
                result.AppendLine("HelperExited: " + exited + " ExitCode=" + (exited ? helper.ExitCode.ToString() : "n/a"));
                passed = exited && helper.ExitCode == 0;
                if (!passed)
                {
                    throw new InvalidOperationException("辅助进程未正常退出");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine("EXCEPTION: " + ex);
                Log.Exception("辅助进程协议测试失败", ex);
            }
            finally
            {
                try { if (writer != null) writer.Dispose(); } catch { }
                try { if (reader != null) reader.Dispose(); } catch { }
                try { if (server != null) server.Dispose(); } catch { }
                try { if (helper != null && !helper.HasExited) helper.Kill(); } catch { }
                try { if (helper != null) helper.Dispose(); } catch { }
            }

            result.AppendLine("RESULT: " + (passed ? "PASS" : "FAIL"));
            string path = Environment.GetEnvironmentVariable("SCREENCURTAIN_RESULT_FILE");
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-guard-protocol-test.txt");
            }
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, result.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "<null>";
            return text.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
