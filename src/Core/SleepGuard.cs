using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScreenCurtain
{
    /// <summary>
    /// Stronger than SetThreadExecutionState: a power request blocks
    /// automatic sleep/hibernate (including Modern Standby idle transitions)
    /// while the blackout is active. User-initiated sleep, lid close and
    /// power-button actions still take precedence.
    /// </summary>
    internal static class SleepGuard
    {
        private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
        private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 1;
        private const int PowerRequestSystemRequired = 0;
        private const int PowerRequestExecutionRequired = 3;

        private static readonly object Sync = new object();
        private static IntPtr _request;
        private static IntPtr _reason;
        private static bool _systemSet;
        private static bool _executionSet;

        public static bool Acquire()
        {
            lock (Sync)
            {
                if (_request != IntPtr.Zero)
                {
                    return true;
                }

                try
                {
                    _reason = Marshal.StringToHGlobalUni("ScreenCurtain blackout");
                    Native.REASON_CONTEXT context = new Native.REASON_CONTEXT();
                    context.Version = POWER_REQUEST_CONTEXT_VERSION;
                    context.Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING;
                    context.Reason = _reason;
                    _request = Native.PowerCreateRequest(ref context);
                    if (_request == IntPtr.Zero)
                    {
                        Log.Warn("PowerCreateRequest 失败，休眠保护不可用，Win32={0}", Marshal.GetLastWin32Error());
                        FreeReason();
                        return false;
                    }

                    _systemSet = Native.PowerSetRequest(_request, PowerRequestSystemRequired);
                    _executionSet = Native.PowerSetRequest(_request, PowerRequestExecutionRequired);
                    if (!_systemSet && !_executionSet)
                    {
                        Log.Warn("PowerSetRequest 失败，休眠保护不可用，Win32={0}", Marshal.GetLastWin32Error());
                        Release();
                        return false;
                    }

                    Log.Info("休眠保护已启用：阻止自动睡眠/休眠");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Exception("启用休眠保护失败", ex);
                    Release();
                    return false;
                }
            }
        }

        public static void Release()
        {
            lock (Sync)
            {
                try
                {
                    if (_request != IntPtr.Zero)
                    {
                        if (_systemSet)
                        {
                            try { Native.PowerClearRequest(_request, PowerRequestSystemRequired); } catch { }
                        }
                        if (_executionSet)
                        {
                            try { Native.PowerClearRequest(_request, PowerRequestExecutionRequired); } catch { }
                        }
                        try { Native.CloseHandle(_request); } catch { }
                    }
                }
                catch
                {
                }
                finally
                {
                    _request = IntPtr.Zero;
                    _systemSet = false;
                    _executionSet = false;
                    FreeReason();
                }
            }
        }

        private static void FreeReason()
        {
            if (_reason != IntPtr.Zero)
            {
                try { Marshal.FreeHGlobal(_reason); } catch { }
                _reason = IntPtr.Zero;
            }
        }
    }
}
