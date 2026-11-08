using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class TopmostGuard : IDisposable
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private readonly List<IntPtr> _targets = new List<IntPtr>();
        private readonly System.Threading.Timer _timer;
        private WinEventDelegate _delegate;
        private IntPtr _foregroundHook;
        private IntPtr _showHook;
        private volatile bool _active;
        private int _refreshing;

        public TopmostGuard()
        {
            _timer = new System.Threading.Timer(Tick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start(List<IntPtr> targets)
        {
            SetTargets(targets);
            if (_active) return;
            _active = true;
            _delegate = OnWinEvent;
            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _delegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            _showHook = SetWinEventHook(
                EVENT_OBJECT_SHOW,
                EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                _delegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            _timer.Change(0, 300);
        }

        public void SetTargets(List<IntPtr> targets)
        {
            lock (_targets)
            {
                _targets.Clear();
                if (targets != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (targets[i] != IntPtr.Zero) _targets.Add(targets[i]);
                    }
                }
            }
        }

        public void Stop()
        {
            _active = false;
            try { _timer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
            try
            {
                if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
            }
            catch { }
            try
            {
                if (_showHook != IntPtr.Zero) UnhookWinEvent(_showHook);
            }
            catch { }
            _foregroundHook = IntPtr.Zero;
            _showHook = IntPtr.Zero;
            lock (_targets) { _targets.Clear(); }
        }

        public void Dispose()
        {
            Stop();
            try { _timer.Dispose(); } catch { }
        }

        private void Tick(object state)
        {
            try { EnsureTopmost(); } catch { }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_active) return;
            // Never run SetWindowPos on the UI thread from a WinEvent callback.
            // A stalled display driver can otherwise freeze the UI heartbeat.
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { EnsureTopmost(); } catch { }
            });
        }

        private void EnsureTopmost()
        {
            if (!_active) return;
            if (Interlocked.Exchange(ref _refreshing, 1) != 0) return;
            try
            {
                List<IntPtr> targets;
                lock (_targets)
                {
                    targets = new List<IntPtr>(_targets);
                }
                for (int i = 0; i < targets.Count; i++)
                {
                    try
                    {
                        if (targets[i] == IntPtr.Zero) continue;
                        Native.SetWindowPos(
                            targets[i],
                            Native.HWND_TOPMOST,
                            0,
                            0,
                            0,
                            0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | Native.SWP_ASYNCWINDOWPOS);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        }
    }
}
