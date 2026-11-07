using System;

namespace ScreenCurtain
{
    internal static class ShutdownGuard
    {
        private static readonly object Sync = new object();
        private static bool _active;
        private static bool _allow;
        private static bool _lastReasonCreated;

        public static string Reason = "幕帘正在阻止 Windows 自动更新重启。请先退出幕帘，或选择“仍要重启”。";

        public static bool LastReasonCreated
        {
            get
            {
                lock (Sync)
                {
                    return _lastReasonCreated;
                }
            }
        }

        public static bool Active
        {
            get
            {
                lock (Sync)
                {
                    return _active;
                }
            }
            set
            {
                lock (Sync)
                {
                    _active = value;
                }
            }
        }

        public static bool AllowShutdown
        {
            get
            {
                lock (Sync)
                {
                    return _allow;
                }
            }
            set
            {
                lock (Sync)
                {
                    _allow = value;
                }
            }
        }

        public static bool HandleQueryEndSession(IntPtr hwnd, uint lParam)
        {
            if (!Active || AllowShutdown)
            {
                return false;
            }

            if ((lParam & Native.ENDSESSION_LOGOFF) != 0)
            {
                Log.Info("收到注销请求，允许本次注销");
                return false;
            }

            if ((lParam & Native.ENDSESSION_CRITICAL) != 0)
            {
                Log.Warn("收到系统严重关机请求，无法阻止");
                return false;
            }

            string reason = Reason;
            bool created = Native.ShutdownBlockReasonCreate(hwnd, reason);
            lock (Sync)
            {
                _lastReasonCreated = created;
            }
            Log.Warn("收到关机/重启请求，已阻止；ShutdownBlockReasonCreate={0}，Win32={1}", created, System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            return true;
        }

        public static void Clear(IntPtr hwnd)
        {
            try
            {
                Native.ShutdownBlockReasonDestroy(hwnd);
            }
            catch
            {
            }
        }
    }
}
