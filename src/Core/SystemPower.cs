using System;
using System.Runtime.InteropServices;

namespace ScreenCurtain
{
    internal static class SystemPower
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        public static bool HasBattery
        {
            get
            {
                // Test hook for desktop machines: lets the laptop path be
                // exercised without physical hardware.
                if (string.Equals(Environment.GetEnvironmentVariable("SCREENCURTAIN_FORCE_BATTERY"), "1", StringComparison.Ordinal))
                {
                    return true;
                }

                try
                {
                    SYSTEM_POWER_STATUS status;
                    if (!GetSystemPowerStatus(out status)) return false;
                    // 128 = no system battery; 255 = unknown.
                    if (status.BatteryFlag == 128 || status.BatteryFlag == 255) return false;
                    return (status.BatteryFlag & 128) == 0;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
