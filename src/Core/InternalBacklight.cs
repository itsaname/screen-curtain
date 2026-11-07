using System;
using System.Management;

namespace ScreenCurtain
{
    /// <summary>
    /// Internal laptop panels generally do not expose DDC/CI luminance.
    /// Windows exposes their backlight through the WMI
    /// WmiMonitorBrightness / WmiMonitorBrightnessMethods classes instead.
    /// This is best-effort: many drivers, OLED panels and HDR modes do not
    /// implement it.
    /// </summary>
    internal static class InternalBacklight
    {
        public static bool TryDimToZero(out byte originalLevel)
        {
            originalLevel = 0;
            try
            {
                bool found = false;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        object value = item["CurrentBrightness"];
                        if (value != null)
                        {
                            originalLevel = Convert.ToByte(value);
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    Log.Info("内置屏 WMI 亮度不可用，保留黑层");
                    return false;
                }

                if (!SetLevel(0))
                {
                    Log.Warn("内置屏 WMI 亮度设为 0 失败");
                    return false;
                }

                Log.Info("内置屏背光已设为 0：原亮度 {0}", originalLevel);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("内置屏 WMI 亮度不可用：{0}", ex.Message);
                return false;
            }
        }

        public static bool SetLevel(byte level)
        {
            try
            {
                bool invoked = false;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        try
                        {
                            item.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, level });
                            invoked = true;
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("WmiSetBrightness 调用失败：{0}", ex.Message);
                        }
                    }
                }
                return invoked;
            }
            catch (Exception ex)
            {
                Log.Warn("内置屏 WMI 亮度方法不可用：{0}", ex.Message);
                return false;
            }
        }

        public static bool Restore(byte level)
        {
            if (SetLevel(level))
            {
                Log.Info("内置屏背光已恢复：{0}", level);
                return true;
            }
            return false;
        }
    }
}
