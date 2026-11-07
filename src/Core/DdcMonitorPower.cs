using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ScreenCurtain
{
    internal static class DdcMonitorPower
    {
        private const byte VCP_POWER = 0xD6;
        private const uint POWER_ON = 0x01;
        // VCP 0xD6: 0x04 is standby and remains wakeable. 0x05 is the true
        // "off" value, but this DisplayPort monitor disappears from Windows and
        // rejects DDC 0x01 wake (tested on both a re-enumerated handle and the
        // retained handle), so sending 0x05 is unsafe for an automatic tool.
        private const uint POWER_OFF = 0x04;
        private const int DDC_TIMEOUT_MS = 2500;

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

        public static List<string> TurnOffExternal()
        {
            List<string> result = null;
            Thread worker = new Thread(delegate()
            {
                result = TurnOffExternalCore();
            });
            worker.IsBackground = true;
            worker.Name = "ScreenCurtain.DdcOff";
            worker.Start();
            if (!worker.Join(DDC_TIMEOUT_MS))
            {
                Log.Warn("DDC 关屏超时（{0}ms）", DDC_TIMEOUT_MS);
                return new List<string>();
            }
            List<string> off = result ?? new List<string>();
            SaveState(off);
            return off;
        }

        // DDC reads often fail while a fullscreen topmost overlay is active,
        // but writes can still work. Use VCP 0xD6=0x04 (standby) only: 0x05 is
        // a deep power-off on tested hardware and cannot be woken via DDC.
        public static List<string> TurnOffExternalStandby()
        {
            List<string> result = null;
            Thread worker = new Thread(delegate()
            {
                result = TurnOffExternalStandbyCore();
            });
            worker.IsBackground = true;
            worker.Name = "ScreenCurtain.DdcStandby";
            worker.Start();
            if (!worker.Join(DDC_TIMEOUT_MS))
            {
                Log.Warn("DDC 待机超时（{0}ms）", DDC_TIMEOUT_MS);
                return new List<string>();
            }
            List<string> off = result ?? new List<string>();
            SaveState(off);
            return off;
        }

        private static List<string> TurnOffExternalStandbyCore()
        {
            List<string> off = new List<string>();
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                Log.Info("DDC 待机候选：{0} 台", displays.Count);
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    if (!d.IsLikelyLaptopExternal) continue;

                    uint count;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(d.Handle, out count) || count == 0) continue;
                    PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                    if (!GetPhysicalMonitorsFromHMONITOR(d.Handle, count, monitors)) continue;
                    bool turnedOff = false;
                    try
                    {
                        for (int j = 0; j < monitors.Length; j++)
                        {
                            // Do not query first: the read path is blocked by a
                            // fullscreen overlay on this driver, but the write
                            // accepts the standby command.
                            if (SetVCPFeature(monitors[j].hPhysicalMonitor, VCP_POWER, POWER_OFF))
                            {
                                turnedOff = true;
                            }
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(count, monitors);
                    }

                    if (turnedOff)
                    {
                        string id = string.IsNullOrEmpty(d.MonitorId) ? d.DeviceName : d.MonitorId;
                        off.Add(id);
                        Log.Info("DDC 外接屏进入待机：{0}（{1:0.0} in）", d.FriendlyName, d.DiagonalInches);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("DDC 外接屏待机失败", ex);
            }
            return off;
        }

        public static void TurnOnExternal(List<string> monitorIds)
        {
            if (monitorIds == null || monitorIds.Count == 0) return;
            List<string> ids = new List<string>(monitorIds);
            bool finished = false;
            Thread worker = new Thread(delegate()
            {
                TurnOnExternalCore(ids);
                finished = true;
            });
            worker.IsBackground = true;
            worker.Name = "ScreenCurtain.DdcOn";
            worker.Start();
            worker.Join(DDC_TIMEOUT_MS);
            if (finished)
            {
                DeleteState();
            }
            else
            {
                Log.Warn("DDC 唤醒超时，保留恢复标记供下次启动重试");
            }
        }

        public static void RecoverStaleState()
        {
            string path = AppPaths.DdcStateFile;
            if (!File.Exists(path)) return;
            try
            {
                List<string> ids = new List<string>();
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    try { ids.Add(Encoding.UTF8.GetString(Convert.FromBase64String(line))); }
                    catch { }
                }
                if (ids.Count > 0)
                {
                    Log.Warn("发现上次未恢复的 DDC 外接屏状态，开始恢复");
                    TurnOnExternal(ids);
                }
                DeleteState();
            }
            catch (Exception ex)
            {
                Log.Exception("恢复遗留 DDC 外接屏状态失败", ex);
            }
        }

        public static string[] Probe()
        {
            List<string> lines = new List<string>();
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    string kind = d.DiagonalInches <= 0
                        ? "unknown-size"
                        : (d.IsLikelyLaptopInternal ? "internal" : (d.IsLikelyLaptopExternal ? "external" : "ambiguous"));
                    string source = string.IsNullOrEmpty(d.DetectionSource)
                        ? "size-fallback"
                        : d.DetectionSource;
                    bool supported = false;
                    uint current = 0;
                    uint maximum = 0;
                    try
                    {
                        uint count;
                        if (GetNumberOfPhysicalMonitorsFromHMONITOR(d.Handle, out count) && count > 0)
                        {
                            PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                            if (GetPhysicalMonitorsFromHMONITOR(d.Handle, count, monitors))
                            {
                                for (int j = 0; j < monitors.Length; j++)
                                {
                                    uint type;
                                    if (GetVCPFeatureAndVCPFeatureReply(monitors[j].hPhysicalMonitor, VCP_POWER, out type, out current, out maximum))
                                    {
                                        supported = true;
                                    }
                                }
                                DestroyPhysicalMonitors(count, monitors);
                            }
                        }
                    }
                    catch
                    {
                    }

                    lines.Add(string.Format(
                        "{0} | {1} | {2:0.0} in | {3} | {4} {5} | DDC power={6} current={7} max={8}",
                        d.DeviceName,
                        d.FriendlyName,
                        d.DiagonalInches,
                        kind,
                        d.DetectionLabel,
                        d.ConnectionTechnology,
                        supported,
                        current,
                        maximum));
                }
            }
            catch (Exception ex)
            {
                lines.Add("DDC probe failed: " + ex.Message);
            }
            return lines.ToArray();
        }

        private static List<string> TurnOffExternalCore()
        {
            List<string> off = new List<string>();
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                Log.Info("DDC 枚举到 {0} 台显示器", displays.Count);
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    Log.Info("DDC 候选：{0}，{1:0.0} in，kind={2}，source={3}，tech={4}",
                        d.DeviceName, d.DiagonalInches, d.DetectionLabel, d.DetectionSource, d.ConnectionTechnology);
                    if (!d.IsLikelyLaptopExternal)
                    {
                        Log.Info("外接屏 DDC 跳过：{0}（{1:0.0} in，内置或尺寸未知）", d.DeviceName, d.DiagonalInches);
                        continue;
                    }

                    uint count;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(d.Handle, out count) || count == 0) continue;
                    PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                    if (!GetPhysicalMonitorsFromHMONITOR(d.Handle, count, monitors)) continue;
                    bool turnedOff = false;
                    try
                    {
                        for (int j = 0; j < monitors.Length; j++)
                        {
                            uint type;
                            uint current;
                            uint maximum;
                            if (!GetVCPFeatureAndVCPFeatureReply(monitors[j].hPhysicalMonitor, VCP_POWER, out type, out current, out maximum))
                            {
                                Log.Warn("外接屏 DDC 电源查询失败：{0}", d.DeviceName);
                                continue;
                            }
                            if (maximum > 0 && (current < 1 || current > maximum))
                            {
                                continue;
                            }

                            bool ok = SetVCPFeature(monitors[j].hPhysicalMonitor, VCP_POWER, POWER_OFF);
                            if (ok) turnedOff = true;
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(count, monitors);
                    }

                    if (turnedOff)
                    {
                        string id = string.IsNullOrEmpty(d.MonitorId) ? d.DeviceName : d.MonitorId;
                        off.Add(id);
                        Log.Info("DDC 关闭外接屏：{0}（{1:0.0} in）", d.FriendlyName, d.DiagonalInches);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("DDC 外接屏关屏失败", ex);
            }
            return off;
        }

        private static void TurnOnExternalCore(List<string> ids)
        {
            try
            {
                List<DisplayInfo> displays = DisplayManager.Enumerate();
                int matched = 0;
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    string id = string.IsNullOrEmpty(d.MonitorId) ? d.DeviceName : d.MonitorId;
                    if (!ids.Contains(id)) continue;
                    if (PowerOn(d)) matched++;
                }

                if (matched == 0)
                {
                    // Monitor IDs can change when a DDC-powered display wakes or
                    // is re-enumerated. Never leave a known-off external panel
                    // dark because of an ID mismatch: fall back to all external
                    // DDC candidates.
                    Log.Warn("DDC 唤醒未匹配到记录，回退唤醒所有外接屏");
                    for (int i = 0; i < displays.Count; i++)
                    {
                        DisplayInfo d = displays[i];
                        if (d.IsLikelyLaptopExternal)
                        {
                            PowerOn(d);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("DDC 外接屏唤醒失败", ex);
            }
        }

        private static bool PowerOn(DisplayInfo d)
        {
            uint count;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(d.Handle, out count) || count == 0) return false;
            PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(d.Handle, count, monitors)) return false;
            try
            {
                for (int j = 0; j < monitors.Length; j++)
                {
                    SetVCPFeature(monitors[j].hPhysicalMonitor, VCP_POWER, POWER_ON);
                }
                Log.Info("DDC 唤醒外接屏：{0}", d.FriendlyName);
                return true;
            }
            finally
            {
                DestroyPhysicalMonitors(count, monitors);
            }
        }

        private static void SaveState(List<string> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    DeleteState();
                    return;
                }
                List<string> lines = new List<string>();
                lines.Add("# ScreenCurtain DDC restore");
                for (int i = 0; i < ids.Count; i++)
                {
                    lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(ids[i])));
                }
                File.WriteAllLines(AppPaths.DdcStateFile, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Exception("保存 DDC 恢复状态失败", ex);
            }
        }

        private static void DeleteState()
        {
            try
            {
                if (File.Exists(AppPaths.DdcStateFile)) File.Delete(AppPaths.DdcStateFile);
            }
            catch
            {
            }
        }
    }
}
