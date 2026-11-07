using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ScreenCurtain
{
    /// <summary>
    /// Recovery-only compatibility for the retired VCP 0xD6 power path.
    /// Older builds could leave a `ddc-restore` marker after putting an
    /// external panel into standby. Current blackout never sends 0xD6, but a
    /// one-shot wake is still useful during upgrade.
    /// </summary>
    internal static class DdcLegacyRecovery
    {
        private const byte VCP_POWER = 0xD6;
        private const uint POWER_ON = 0x01;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint count,
            [Out] PHYSICAL_MONITOR[] monitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyPhysicalMonitors(uint count, PHYSICAL_MONITOR[] monitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

        public static void RecoverStaleState()
        {
            if (!File.Exists(AppPaths.DdcStateFile)) return;
            try
            {
                Log.Warn("发现旧版 DDC 电源恢复标记，尝试唤醒外接屏");
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo display = displays[i];
                    if (!display.IsLikelyLaptopExternal) continue;

                    uint count;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(display.Handle, out count) || count == 0) continue;
                    PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                    if (!GetPhysicalMonitorsFromHMONITOR(display.Handle, count, monitors)) continue;
                    try
                    {
                        for (int j = 0; j < monitors.Length; j++)
                        {
                            SetVCPFeature(monitors[j].hPhysicalMonitor, VCP_POWER, POWER_ON);
                        }
                        Log.Info("旧版 DDC 标记：已请求唤醒 {0}", display.FriendlyName);
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(count, monitors);
                    }
                }
                File.Delete(AppPaths.DdcStateFile);
            }
            catch (Exception ex)
            {
                Log.Exception("旧版 DDC 恢复失败", ex);
            }
        }
    }
}
