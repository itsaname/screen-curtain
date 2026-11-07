using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ScreenCurtain
{
    internal sealed class BacklightState
    {
        public string MonitorId;
        public string DeviceName;
        public uint Level;
        public bool IsInternal;
    }

    /// <summary>
    /// Turns off only the external panel backlight by setting DDC/CI luminance
    /// (VCP 0x10) to 0. The monitor stays powered, so its USB hub and input
    /// devices remain alive. The original luminance is restored on wake.
    /// No VCP 0xD6 power-mode command is used.
    /// </summary>
    internal static class DdcBacklight
    {
        private const byte VCP_BRIGHTNESS = 0x10;

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
        private static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor,
            byte code,
            out uint type,
            out uint current,
            out uint maximum);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

        public static string[] Probe()
        {
            List<string> lines = new List<string>();
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    if (!d.IsLikelyLaptopExternal) continue;
                    uint count;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(d.Handle, out count) || count == 0) continue;
                    PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                    if (!GetPhysicalMonitorsFromHMONITOR(d.Handle, count, monitors)) continue;
                    try
                    {
                        for (int j = 0; j < monitors.Length; j++)
                        {
                            uint type;
                            uint current;
                            uint maximum;
                            bool ok = GetVCPFeatureAndVCPFeatureReply(monitors[j].hPhysicalMonitor, VCP_BRIGHTNESS,
                                out type, out current, out maximum);
                            lines.Add(string.Format("{0} | {1} | brightness={2} current={3} max={4}",
                                d.DeviceName, d.FriendlyName, ok, current, maximum));
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(count, monitors);
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add("probe failed: " + ex.Message);
            }
            return lines.ToArray();
        }

        public static List<BacklightState> DimExternal()
        {            List<BacklightState> states = new List<BacklightState>();
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    if (!d.IsLikelyLaptopExternal) continue;

                    uint count;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(d.Handle, out count) || count == 0) continue;
                    PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                    if (!GetPhysicalMonitorsFromHMONITOR(d.Handle, count, monitors)) continue;
                    try
                    {
                        for (int j = 0; j < monitors.Length; j++)
                        {
                            uint type;
                            uint current;
                            uint maximum;
                            if (!GetVCPFeatureAndVCPFeatureReply(monitors[j].hPhysicalMonitor, VCP_BRIGHTNESS,
                                    out type, out current, out maximum))
                            {
                                Log.Warn("外接屏不支持 DDC 亮度：{0}", d.DeviceName);
                                continue;
                            }
                            if (maximum == 0) continue; // not a writable brightness control

                            if (!SetVCPFeature(monitors[j].hPhysicalMonitor, VCP_BRIGHTNESS, 0))
                            {
                                Log.Warn("设置外接屏亮度为 0 失败：{0}", d.DeviceName);
                                continue;
                            }

                            BacklightState state = new BacklightState();
                            state.MonitorId = string.IsNullOrEmpty(d.MonitorId) ? d.DeviceName : d.MonitorId;
                            state.DeviceName = d.DeviceName;
                            state.Level = current;
                            states.Add(state);
                            Log.Info("外接屏背光已设为 0：{0}（原亮度 {1}/{2}）",
                                d.FriendlyName, current, maximum);
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(count, monitors);
                    }
                }

                SaveStates(states);
            }
            catch (Exception ex)
            {
                Log.Exception("关闭外接屏背光失败", ex);
            }

            try
            {
                byte internalLevel;
                if (InternalBacklight.TryDimToZero(out internalLevel))
                {
                    BacklightState internalState = new BacklightState();
                    internalState.MonitorId = "WMI:INTERNAL";
                    internalState.DeviceName = "internal";
                    internalState.Level = internalLevel;
                    internalState.IsInternal = true;
                    states.Add(internalState);
                    SaveStates(states);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("关闭内置屏背光失败", ex);
            }

            return states;
        }

        public static void Restore(List<BacklightState> states)
        {
            if (states == null || states.Count == 0)
            {
                RecoverStaleState();
                return;
            }

            try
            {
                bool allOk = true;
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                for (int i = 0; i < states.Count; i++)
                {
                    BacklightState state = states[i];
                    if (state.IsInternal || state.MonitorId == "WMI:INTERNAL")
                    {
                        if (!InternalBacklight.Restore((byte)state.Level))
                        {
                            allOk = false;
                        }
                        continue;
                    }

                    DisplayInfo display = FindDisplay(displays, state);
                    if (display == null)
                    {
                        allOk = false;
                        continue;
                    }

                    if (!RestoreOne(display, state.Level))
                    {
                        allOk = false;
                    }
                }

                if (allOk)
                {
                    DeleteState();
                }
                else
                {
                    Log.Warn("部分外接屏亮度恢复失败，保留状态标记");
                }
            }
            catch (Exception ex)
            {
                Log.Exception("恢复外接屏背光失败", ex);
            }
        }

        public static void RecoverStaleState()
        {
            string path = AppPaths.BacklightStateFile;
            if (!File.Exists(path)) return;
            try
            {
                List<BacklightState> states = new List<BacklightState>();
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length < 2) continue;
                    BacklightState state = new BacklightState();
                    try
                    {
                        state.MonitorId = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                        state.Level = Convert.ToUInt32(parts[1]);
                    }
                    catch
                    {
                        continue;
                    }
                    state.IsInternal = state.MonitorId == "WMI:INTERNAL";
                    states.Add(state);
                }

                if (states.Count == 0)
                {
                    DeleteState();
                    return;
                }

                Log.Warn("发现上次未恢复的外接屏背光状态，开始恢复");
                Restore(states);
            }
            catch (Exception ex)
            {
                Log.Exception("恢复遗留背光状态失败", ex);
            }
        }

        private static DisplayInfo FindDisplay(List<DisplayInfo> displays, BacklightState state)
        {
            for (int i = 0; i < displays.Count; i++)
            {
                DisplayInfo d = displays[i];
                string id = string.IsNullOrEmpty(d.MonitorId) ? d.DeviceName : d.MonitorId;
                if (id == state.MonitorId) return d;
            }
            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i].DeviceName == state.DeviceName) return displays[i];
            }
            return null;
        }

        private static bool RestoreOne(DisplayInfo display, uint level)
        {
            uint count;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(display.Handle, out count) || count == 0) return false;
            PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(display.Handle, count, monitors)) return false;
            try
            {
                bool ok = false;
                for (int i = 0; i < monitors.Length; i++)
                {
                    if (SetVCPFeature(monitors[i].hPhysicalMonitor, VCP_BRIGHTNESS, level))
                    {
                        ok = true;
                    }
                }
                if (ok)
                {
                    Log.Info("外接屏背光已恢复：{0} -> {1}", display.FriendlyName, level);
                }
                return ok;
            }
            finally
            {
                DestroyPhysicalMonitors(count, monitors);
            }
        }

        private static void SaveStates(List<BacklightState> states)
        {
            try
            {
                if (states == null || states.Count == 0)
                {
                    DeleteState();
                    return;
                }
                List<string> lines = new List<string>();
                lines.Add("# ScreenCurtain backlight restore (base64 monitor id|level)");
                for (int i = 0; i < states.Count; i++)
                {
                    lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(states[i].MonitorId)) +
                              "|" + states[i].Level);
                }
                File.WriteAllLines(AppPaths.BacklightStateFile, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Exception("保存背光恢复状态失败", ex);
            }
        }

        private static void DeleteState()
        {
            try
            {
                if (File.Exists(AppPaths.BacklightStateFile)) File.Delete(AppPaths.BacklightStateFile);
            }
            catch
            {
            }
        }
    }
}
