using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScreenCurtain
{
    internal sealed class DisplayInfo
    {
        public IntPtr Handle;
        public string DeviceName;
        public string FriendlyName;
        public string MonitorId;
        public Native.RECT Bounds;
        public bool Primary;
        public int PhysicalWidthMm;
        public int PhysicalHeightMm;
        public double DiagonalInches;
        public DisplayConnectionKind ConnectionKind = DisplayConnectionKind.Unknown;
        public string ConnectionTechnology = string.Empty;
        public string DetectionSource = string.Empty;

        public bool IsLikelyLaptopInternal
        {
            get
            {
                if (ConnectionKind == DisplayConnectionKind.Internal) return true;
                if (ConnectionKind == DisplayConnectionKind.External) return false;
                return SystemPower.HasBattery &&
                       DiagonalInches > 0 &&
                       DiagonalInches <= DisplayManager.LaptopInternalMaxInches;
            }
        }

        public bool IsLikelyLaptopExternal
        {
            get
            {
                if (ConnectionKind == DisplayConnectionKind.External) return true;
                if (ConnectionKind == DisplayConnectionKind.Internal) return false;
                return SystemPower.HasBattery &&
                       DiagonalInches > DisplayManager.LaptopInternalMaxInches;
            }
        }

        public string DetectionLabel
        {
            get
            {
                if (ConnectionKind == DisplayConnectionKind.Internal) return "内置";
                if (ConnectionKind == DisplayConnectionKind.External) return "外接";
                return "未知";
            }
        }
    }

    internal static class DisplayManager
    {
        // Laptop internal panels are practically never larger than this.
        // Size is used instead of the primary-display flag, which is unstable
        // when the user makes an external monitor primary.
        public const double LaptopInternalMaxInches = 21.0;

        public static List<DisplayInfo> Enumerate()
        {
            List<DisplayInfo> result = new List<DisplayInfo>();
            Native.MonitorEnumProc callback = delegate(IntPtr hMonitor, IntPtr hdc, ref Native.RECT monitorRect, IntPtr data)
            {
                Native.MONITORINFO info = new Native.MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
                bool got = Native.GetMonitorInfo(hMonitor, ref info);

                DisplayInfo display = new DisplayInfo();
                display.Handle = hMonitor;
                if (got)
                {
                    display.Bounds = info.rcMonitor;
                    display.DeviceName = info.szDevice;
                    display.Primary = (info.dwFlags & 1) != 0;
                }
                else
                {
                    display.Bounds = monitorRect;
                    display.DeviceName = "\\\\.\\DISPLAY";
                    display.Primary = false;
                }
                EnrichDisplayInfo(display);
                result.Add(display);
                return true;
            };

            try
            {
                Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Exception("枚举显示器失败", ex);
            }

            result.Sort(delegate(DisplayInfo a, DisplayInfo b)
            {
                int cmp = a.Bounds.Left.CompareTo(b.Bounds.Left);
                if (cmp != 0) return cmp;
                cmp = a.Bounds.Top.CompareTo(b.Bounds.Top);
                if (cmp != 0) return cmp;
                return string.Compare(a.DeviceName, b.DeviceName, StringComparison.OrdinalIgnoreCase);
            });

            ApplyDisplayConfig(result);
            return result;
        }

        private static void ApplyDisplayConfig(List<DisplayInfo> displays)
        {
            try
            {
                List<DisplayTargetInfo> targets = DisplayConfigReader.GetActiveTargets();
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo display = displays[i];
                    DisplayTargetInfo match = null;
                    for (int j = 0; j < targets.Count; j++)
                    {
                        if (targets[j].X == display.Bounds.Left && targets[j].Y == display.Bounds.Top)
                        {
                            match = targets[j];
                            break;
                        }
                    }

                    if (match == null)
                    {
                        for (int j = 0; j < targets.Count; j++)
                        {
                            if (targets[j].Width == display.Bounds.Width &&
                                targets[j].Height == display.Bounds.Height)
                            {
                                match = targets[j];
                                break;
                            }
                        }
                    }

                    if (match == null) continue;
                    display.ConnectionKind = match.Kind;
                    display.ConnectionTechnology = match.TechnologyName;
                    display.DetectionSource = "DisplayConfig";
                    if (string.IsNullOrEmpty(display.FriendlyName) && !string.IsNullOrEmpty(match.FriendlyName))
                    {
                        display.FriendlyName = match.FriendlyName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("应用 DisplayConfig 显示器连接类型失败", ex);
            }
        }

        private static void EnrichDisplayInfo(DisplayInfo display)
        {
            try
            {
                Native.DISPLAY_DEVICE dd = new Native.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                if (Native.EnumDisplayDevices(display.DeviceName, 0, ref dd, 0))
                {
                    display.FriendlyName = dd.DeviceString;
                    display.MonitorId = dd.DeviceID;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("读取显示器名称失败", ex);
            }

            try
            {
                IntPtr dc = Native.CreateDC(null, display.DeviceName, null, IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    try
                    {
                        int widthMm = Native.GetDeviceCaps(dc, 4);   // HORZSIZE
                        int heightMm = Native.GetDeviceCaps(dc, 6);  // VERTSIZE
                        if (widthMm > 0 && heightMm > 0)
                        {
                            display.PhysicalWidthMm = widthMm;
                            display.PhysicalHeightMm = heightMm;
                            display.DiagonalInches = Math.Sqrt((double)widthMm * widthMm + (double)heightMm * heightMm) / 25.4;
                        }
                    }
                    finally
                    {
                        Native.DeleteDC(dc);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("读取显示器物理尺寸失败", ex);
            }
        }
    }
}
