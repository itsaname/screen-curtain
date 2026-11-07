using System;
using System.Runtime.InteropServices;

namespace ScreenCurtain
{
    internal static class MonitorPower
    {
        public static bool TrySet(bool on)
        {
            try
            {
                IntPtr result;
                IntPtr messageResult = Native.SendMessageTimeout(
                    new IntPtr(0xFFFF),
                    (uint)Native.WM_SYSCOMMAND,
                    new IntPtr(Native.SC_MONITORPOWER),
                    new IntPtr(on ? Native.MONITOR_ON : Native.MONITOR_OFF),
                    Native.SMTO_ABORTIFHUNG,
                    1000,
                    out result);

                if (messageResult == IntPtr.Zero)
                {
                    Log.Warn("SC_MONITORPOWER 广播失败，Win32={0}", Marshal.GetLastWin32Error());
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("SC_MONITORPOWER 调用异常", ex);
                return false;
            }
        }
    }
}
