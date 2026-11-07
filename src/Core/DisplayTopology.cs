using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ScreenCurtain
{
    /// <summary>
    /// Turns external panels off at the display-output level instead of using
    /// DDC/CI full power-off. The external monitor loses signal and enters
    /// standby, but keeps its power supply and can wake when the output is
    /// restored. The built-in panel stays active under the black overlay.
    /// </summary>
    internal static class DisplayTopology
    {
        public static bool DisableExternalOutputs()
        {
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                bool hasInternal = false;
                bool hasExternal = false;
                bool allSameBounds = true;
                for (int i = 0; i < displays.Count; i++)
                {
                    if (displays[i].IsLikelyLaptopInternal) hasInternal = true;
                    if (displays[i].IsLikelyLaptopExternal) hasExternal = true;
                    if (i > 0)
                    {
                        Native.RECT a = displays[0].Bounds;
                        Native.RECT b = displays[i].Bounds;
                        if (a.Left != b.Left || a.Top != b.Top ||
                            a.Right != b.Right || a.Bottom != b.Bottom)
                        {
                            allSameBounds = false;
                        }
                    }
                }

                if (!hasInternal || !hasExternal)
                {
                    return false;
                }

                string restoreMode = allSameBounds ? "clone" : "extend";
                File.WriteAllText(AppPaths.TopologyStateFile, restoreMode, new UTF8Encoding(false));
                Log.Info("关闭外接显示输出，保留内置屏；fallback={0}", restoreMode);

                if (!DisplayConfigReader.TryDisableExternalOutputs())
                {
                    DeleteState();
                    return false;
                }

                Thread.Sleep(700);
                List<DisplayInfo> after = DisplayManager.Enumerate();
                bool externalStillActive = false;
                for (int i = 0; i < after.Count; i++)
                {
                    if (after[i].IsLikelyLaptopExternal) externalStillActive = true;
                }
                if (externalStillActive)
                {
                    Log.Warn("切换到仅内置屏后仍检测到外接屏，回退");
                    DisplayConfigReader.TryRestoreExternalOutputs();
                    DeleteState();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("关闭外接显示输出失败", ex);
                DisplayConfigReader.TryRestoreExternalOutputs();
                DeleteState();
                return false;
            }
        }

        public static void RestoreExternalOutputs()
        {
            string mode = "extend";
            try
            {
                if (File.Exists(AppPaths.TopologyStateFile))
                {
                    string saved = File.ReadAllText(AppPaths.TopologyStateFile, Encoding.UTF8).Trim();
                    if (saved == "clone" || saved == "extend")
                    {
                        mode = saved;
                    }
                }
                Log.Info("恢复外接显示输出");
                if (!DisplayConfigReader.TryRestoreExternalOutputs())
                {
                    // Reaching this path normally means the in-memory topology
                    // snapshot is gone (for example after a crash). The external
                    // panel is still powered; this is only a recovery fallback.
                    Log.Warn("SetDisplayConfig 快照不可用，尝试扩展桌面");
                    RunDisplaySwitch(mode);
                }
                DeleteState();
            }
            catch (Exception ex)
            {
                Log.Exception("恢复外接显示输出失败", ex);
            }
        }

        public static void RecoverStaleState()
        {
            if (!File.Exists(AppPaths.TopologyStateFile)) return;
            Log.Warn("发现上次未恢复的外接显示输出状态，开始恢复");
            RestoreExternalOutputs();
        }

        private static bool RunDisplaySwitch(string mode)
        {
            try
            {
                string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "DisplaySwitch.exe");
                if (!File.Exists(exe)) return false;
                ProcessStartInfo psi = new ProcessStartInfo(exe, "/" + mode);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process process = Process.Start(psi))
                {
                    if (process == null) return false;
                    if (!process.WaitForExit(6000))
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("调用 DisplaySwitch 失败：" + mode, ex);
                return false;
            }
        }

        private static void DeleteState()
        {
            try
            {
                if (File.Exists(AppPaths.TopologyStateFile)) File.Delete(AppPaths.TopologyStateFile);
            }
            catch
            {
            }
        }
    }
}
