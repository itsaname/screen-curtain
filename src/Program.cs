using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class Program
    {
        private const string MutexName = "Local\\ScreenCurtain.SingleInstance";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Native.EnablePerMonitorDpi();

            if (HasArgument(args, "--request-exit"))
            {
                AppMessages.RequestExistingExit();
                return;
            }

            if (HasArgument(args, "--repair-update"))
            {
                PolicyRepair.Run();
                return;
            }

            if (HasArgument(args, "--update-guard"))
            {
                UpdateGuardServer.Run(args);
                return;
            }

            if (HasArgument(args, "--self-test"))
            {
                SelfTest.Run();
                return;
            }

            if (HasArgument(args, "--test-audio"))
            {
                AudioTest.Run();
                return;
            }

            if (HasArgument(args, "--test-backlight"))
            {
                BacklightTest.Run();
                return;
            }

            if (HasArgument(args, "--dry-run"))
            {
                DryRun.Run();
                return;
            }

            if (HasArgument(args, "--test-blackout"))
            {
                BlackoutTestHarness.Run();
                return;
            }

            if (HasArgument(args, "--test-policy"))
            {
                PolicyTest.Run();
                return;
            }

            if (HasArgument(args, "--test-guard-protocol"))
            {
                GuardProtocolTest.Run();
                return;
            }

            if (HasArgument(args, "--test-shutdown-guard"))
            {
                ShutdownGuardTestForm.Run();
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                Log.Exception("未处理的 UI 异常", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Exception("未处理的进程异常", e.ExceptionObject as Exception);
            };

            bool createdNew = false;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    ActivateExistingWindow();
                    return;
                }

                try
                {
                    AudioMuteService.RecoverStaleState();
                    DdcLegacyRecovery.RecoverStaleState();
                    DdcBacklight.RecoverStaleState();
                    Application.Run(new AppHost());
                }
                catch (Exception ex)
                {
                    Log.Exception("主程序异常退出", ex);
                    MessageBox.Show("幕帘发生错误，已写入日志。\r\n" + ex.Message, "幕帘", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
            }
        }

        private static bool HasArgument(string[] args, string value)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ActivateExistingWindow()
        {
            if (AppMessages.ActivateExisting()) return;
            MessageBox.Show("幕帘已经在运行。", "幕帘", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    internal static class PolicyRepair
    {
        public static void Run()
        {
            try
            {
                bool hadBackup = PolicySnapshot.HasBackup();
                PolicyManager.RecoverOrphaned();
                Log.Info("卸载前策略修复完成；存在备份={0}", hadBackup);
            }
            catch (Exception ex)
            {
                Log.Exception("恢复遗留 Windows Update 策略失败", ex);
                try
                {
                    MessageBox.Show(
                        "更新设置恢复失败，请查看日志后重试。\r\n" + ex.Message,
                        "幕帘 - 恢复更新设置",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch { }
                Environment.ExitCode = 2;
            }
        }
    }

    internal static class AudioTest
    {
        public static void Run()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                List<AudioEndpointState> endpoints = NativeAudio.GetActiveRenderEndpoints();
                sb.AppendLine("ScreenCurtain audio mute test");
                sb.AppendLine("Endpoints: " + endpoints.Count);
                for (int i = 0; i < endpoints.Count; i++)
                {
                    sb.AppendLine("  " + endpoints[i].Id + " muted=" + endpoints[i].Muted);
                }

                Settings settings = new Settings();
                settings.MuteSystemAudio = true;
                AudioMuteService service = new AudioMuteService(settings);
                service.MuteForBlackout();
                System.Threading.Thread.Sleep(250);
                bool allMuted = true;
                List<AudioEndpointState> muted = NativeAudio.GetActiveRenderEndpoints();
                for (int i = 0; i < muted.Count; i++)
                {
                    if (!muted[i].Muted) allMuted = false;
                }
                service.Restore();
                System.Threading.Thread.Sleep(250);

                bool restored = true;
                List<AudioEndpointState> after = NativeAudio.GetActiveRenderEndpoints();
                for (int i = 0; i < after.Count; i++)
                {
                    bool expected = false;
                    for (int j = 0; j < endpoints.Count; j++)
                    {
                        if (endpoints[j].Id == after[i].Id) { expected = endpoints[j].Muted; break; }
                    }
                    if (after[i].Muted != expected) restored = false;
                }

                sb.AppendLine("Muted during blackout: " + allMuted);
                sb.AppendLine("Restored original mute state: " + restored);
                sb.AppendLine("Result: " + (endpoints.Count == 0 ? "SKIP (no active playback devices)" : (allMuted && restored ? "PASS" : "FAIL")));
            }
            catch (Exception ex)
            {
                sb.AppendLine("Result: FAIL");
                sb.AppendLine(ex.ToString());
                Log.Exception("音频测试失败", ex);
            }

            string path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-audio-test.txt");
            try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }
    }

    internal static class BacklightTest
    {
        public static void Run()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                sb.AppendLine("ScreenCurtain external backlight test");
                string[] before = DdcBacklight.Probe();
                for (int i = 0; i < before.Length; i++) sb.AppendLine("before " + before[i]);
                List<BacklightState> states = DdcBacklight.DimExternal();
                sb.AppendLine("dimmed monitors: " + states.Count);
                System.Threading.Thread.Sleep(2000);
                string[] during = DdcBacklight.Probe();
                for (int i = 0; i < during.Length; i++) sb.AppendLine("during " + during[i]);
                DdcBacklight.Restore(states);
                System.Threading.Thread.Sleep(1000);
                string[] after = DdcBacklight.Probe();
                for (int i = 0; i < after.Length; i++) sb.AppendLine("after " + after[i]);
                sb.AppendLine("Result: PASS");
            }
            catch (Exception ex)
            {
                sb.AppendLine("Result: FAIL");
                sb.AppendLine(ex.ToString());
                Log.Exception("背光测试失败", ex);
            }

            string path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-backlight-test.txt");
            try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }
    }

    internal static class SelfTest
    {
        public static void Run()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                sb.AppendLine("ScreenCurtain self-test");
                sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("OS: " + Environment.OSVersion.VersionString);
                sb.AppendLine("Runtime: " + Environment.Version);
                sb.AppendLine("64-bit process: " + Environment.Is64BitProcess);
                sb.AppendLine("Admin: " + IsAdmin());
                sb.AppendLine("DPI awareness call: " + Native.EnablePerMonitorDpi());
                sb.AppendLine();

                List<DisplayInfo> displays = DisplayManager.Enumerate();
                sb.AppendLine("Displays: " + displays.Count);
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    sb.AppendFormat(
                        "  [{0}] {1} primary={2} rect={3},{4} {5}x{6}\r\n",
                        i,
                        d.DeviceName,
                        d.Primary,
                        d.Bounds.Left,
                        d.Bounds.Top,
                        d.Bounds.Width,
                        d.Bounds.Height);
                }
                sb.AppendLine();

                Cursor c = TransparentCursorFactory.Get();
                sb.AppendLine("Transparent cursor: " + (c != null ? "created" : "null"));
                sb.AppendLine("Settings: " + Settings.FilePath);
                sb.AppendLine("Log: " + Log.CurrentFile);
                sb.AppendLine("SetThreadExecutionState test: " + Native.SetThreadExecutionState(Native.ES_CONTINUOUS));
                sb.AppendLine("Result: PASS");
            }
            catch (Exception ex)
            {
                sb.AppendLine("Result: FAIL");
                sb.AppendLine(ex.ToString());
                Log.Exception("self-test 失败", ex);
            }

            string path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-selftest.txt");
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Log.Info("self-test 输出：{0}", path);
            }
            catch
            {
            }
        }

        private static bool IsAdmin()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
