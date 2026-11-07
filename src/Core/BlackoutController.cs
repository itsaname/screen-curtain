using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal enum BlackoutState
    {
        Idle,
        Arming,
        Dark,
        Restoring
    }

    internal sealed class BlackoutController : IDisposable
    {
        private readonly Control _dispatcher;
        private readonly Settings _settings;
        private readonly object _sync = new object();
        private readonly List<OverlayForm> _overlays = new List<OverlayForm>();
        private readonly Dictionary<string, OverlayForm> _overlayByKey = new Dictionary<string, OverlayForm>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Thread _watchdog;
        private readonly AudioMuteService _audio;
        private readonly TopmostGuard _topmostGuard;
        private volatile bool _watchdogStop;
        private long _heartbeatTicks;
        private int _stateValue;
        private bool _disposed;
        private bool _wakePending;
        private bool _screenOffSent;
        private bool _physicalOffRequested;
        private List<BacklightState> _backlightStates = new List<BacklightState>();
        private DateTime _startedAt;
        private DateTime _armAt;
        private DateTime _lastExecutionStateRefreshUtc;
        private IntPtr _previousForeground;
        private Cursor _cursor;
        private Native.HookProc _mouseProc;
        private Native.HookProc _keyProc;
        private IntPtr _mouseHook;
        private IntPtr _keyHook;
        private string _lastError;

        public event Action<BlackoutState> StateChanged;
        public event Action<string> Error;

        public BlackoutController(Control dispatcher, Settings settings)
        {
            if (dispatcher == null) throw new ArgumentNullException("dispatcher");
            if (settings == null) throw new ArgumentNullException("settings");

            _dispatcher = dispatcher;
            _settings = settings;
            _audio = new AudioMuteService(settings);
            _topmostGuard = new TopmostGuard();
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 50;
            _timer.Tick += TimerTick;

            _watchdog = new Thread(WatchdogLoop);
            _watchdog.IsBackground = true;
            _watchdog.Name = "ScreenCurtain.Watchdog";
            _watchdog.Start();
        }

        public BlackoutState State
        {
            get
            {
                lock (_sync)
                {
                    return (BlackoutState)_stateValue;
                }
            }
        }

        public bool IsDark
        {
            get { return State == BlackoutState.Dark; }
        }

        public bool IsArming
        {
            get { return State == BlackoutState.Arming; }
        }

        public string LastError
        {
            get { lock (_sync) { return _lastError; } }
        }

        // Test hook: suppresses only the physical DPMS request. The black
        // overlays and input hooks still run normally.
        public bool SuppressMonitorPower
        {
            get { return _suppressMonitorPower; }
            set { _suppressMonitorPower = value; }
        }

        private volatile bool _suppressMonitorPower;

        public bool Enter()
        {
            lock (_sync)
            {
                if (_disposed || _stateValue != (int)BlackoutState.Idle)
                {
                    return false;
                }
            }

            List<DisplayInfo> displays = DisplayManager.Enumerate();
            if (displays.Count == 0)
            {
                Fail("没有检测到活动显示器，无法熄屏");
                return false;
            }

            lock (_sync)
            {
                if (_stateValue != (int)BlackoutState.Idle)
                {
                    return false;
                }
                _stateValue = (int)BlackoutState.Arming;
            }

            _previousForeground = Native.GetForegroundWindow();
            _cursor = TransparentCursorFactory.Get();
            _screenOffSent = false;
            _physicalOffRequested = false;
            _backlightStates.Clear();
            _wakePending = false;
            _startedAt = DateTime.UtcNow;
            _armAt = _startedAt.AddMilliseconds(700);
            _lastExecutionStateRefreshUtc = _startedAt;
            Interlocked.Exchange(ref _heartbeatTicks, DateTime.UtcNow.Ticks);

            // Dim external backlights before creating the overlay. Brightness 0
            // keeps the monitor powered (USB hub/input stays alive), unlike
            // VCP 0xD6 power-off. The overlay itself does not change luminance.
            if (ShouldDimExternalBacklight())
            {
                _backlightStates = DdcBacklight.DimExternal();
                Log.Info("外接屏背光关闭：{0} 台（显示器保持供电/输入可用）", _backlightStates.Count);
            }

            List<OverlayForm> created = new List<OverlayForm>();
            try
            {
                for (int i = 0; i < displays.Count; i++)
                {
                    OverlayForm overlay = new OverlayForm(this, displays[i], _cursor);
                    overlay.Show();
                    created.Add(overlay);
                }

                Native.DwmFlush();

                lock (_sync)
                {
                    _overlays.Clear();
                    _overlays.AddRange(created);
                    _overlayByKey.Clear();
                    for (int i = 0; i < created.Count; i++)
                    {
                        _overlayByKey[GetDisplayKey(created[i].Display)] = created[i];
                    }
                    _stateValue = (int)BlackoutState.Arming;
                    _lastError = null;
                }

                uint flags = Native.ES_CONTINUOUS;
                if (_settings.PreventSleep)
                {
                    flags |= Native.ES_SYSTEM_REQUIRED | Native.ES_DISPLAY_REQUIRED;
                }
                if (Native.SetThreadExecutionState(flags) == 0)
                {
                    Log.Warn("SetThreadExecutionState 失败，Win32={0}", Marshal.GetLastWin32Error());
                }
                _lastExecutionStateRefreshUtc = DateTime.UtcNow;

                _topmostGuard.Start(GetOverlayHandles());
                _audio.MuteForBlackout();
                InstallHooks();
                FocusPrimaryOverlay();
                _timer.Start();
                RaiseState(BlackoutState.Arming);
                Log.Info("进入熄屏准备：{0} 台显示器，PreventSleep={1}", created.Count, _settings.PreventSleep);
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("进入熄屏失败", ex);
                try { _timer.Stop(); } catch { }
                try { _topmostGuard.Stop(); } catch { }
                try { _audio.Restore(); } catch { }
                UninstallHooks();
                for (int i = 0; i < created.Count; i++)
                {
                    try { created[i].Close(); created[i].Dispose(); } catch { }
                }
                if (_backlightStates.Count > 0)
                {
                    try { DdcBacklight.Restore(_backlightStates); } catch { }
                    _backlightStates.Clear();
                }
                Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
                lock (_sync)
                {
                    _overlays.Clear();
                    _overlayByKey.Clear();
                    _stateValue = (int)BlackoutState.Idle;
                    _lastError = "进入熄屏失败：" + ex.Message;
                    Interlocked.Exchange(ref _heartbeatTicks, 0);
                }
                RaiseState(BlackoutState.Idle);
                Fail("进入熄屏失败：" + ex.Message);
                return false;
            }
        }

        public void Restore(string reason)
        {
            bool didWork = false;
            lock (_sync)
            {
                if (_stateValue == (int)BlackoutState.Idle || _stateValue == (int)BlackoutState.Restoring)
                {
                    return;
                }
                _stateValue = (int)BlackoutState.Restoring;
                _wakePending = false;
                didWork = true;
            }

            if (!didWork)
            {
                return;
            }

            Log.Info("恢复显示：{0}", reason ?? "未指定");
            RaiseState(BlackoutState.Restoring);

            try
            {
                _timer.Stop();
            }
            catch
            {
            }

            UninstallHooks();
            Interlocked.Exchange(ref _heartbeatTicks, 0);
            try { _topmostGuard.Stop(); } catch { }
            if (_physicalOffRequested)
            {
                // Only turn displays back on if this blackout actually sent the
                // physical-off request. Otherwise an ON broadcast can wake a
                // laptop panel that was intentionally left alone.
                MonitorPower.TrySet(true);
                _physicalOffRequested = false;
            }

            if (Native.SetThreadExecutionState(Native.ES_CONTINUOUS) == 0)
            {
                Log.Warn("清除 SetThreadExecutionState 失败，Win32={0}", Marshal.GetLastWin32Error());
            }

            List<OverlayForm> toClose;
            lock (_sync)
            {
                toClose = new List<OverlayForm>(_overlays);
                _overlays.Clear();
                _overlayByKey.Clear();
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                try
                {
                    toClose[i].Close();
                    toClose[i].Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception("关闭覆盖窗口失败", ex);
                }
            }

            try
            {
                if (_previousForeground != IntPtr.Zero &&
                    _settings.RestoreForeground &&
                    Native.IsWindow(_previousForeground) &&
                    _previousForeground != _dispatcher.Handle)
                {
                    Native.SetForegroundWindow(_previousForeground);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("恢复前台窗口失败", ex);
            }

            // Restore the external backlight only after the fullscreen overlays
            // are gone. The monitor was never powered off, so its USB hub and
            // input devices stayed available throughout the blackout.
            if (_backlightStates.Count > 0)
            {
                Log.Info("恢复 {0} 台外接屏背光", _backlightStates.Count);
                DdcBacklight.Restore(_backlightStates);
                _backlightStates.Clear();
            }
            else
            {
                DdcBacklight.RecoverStaleState();
            }

            // Restore audio after the display is on and the black layer has
            // been removed. Audio endpoints may be suspended/reset while the
            // display is off; unmuting earlier can be overwritten by the
            // endpoint's later resume. AudioMuteService also retries and keeps
            // its recovery marker until verification succeeds.
            try { _audio.Restore(); } catch (Exception ex) { Log.Exception("恢复音频失败", ex); }

            IntPtr hwnd = IntPtr.Zero;
            try
            {
                if (_dispatcher.IsHandleCreated)
                {
                    hwnd = _dispatcher.Handle;
                }
            }
            catch
            {
            }
            // ShutdownGuard 的阻止原因由主窗口在保护关闭时统一清理；
            // 退出 Dark 不应取消仍在生效的更新保护。

            lock (_sync)
            {
                _stateValue = (int)BlackoutState.Idle;
            }
            RaiseState(BlackoutState.Idle);
            Log.Info("恢复完成");
        }

        public void SafeRestore(string reason)
        {
            try
            {
                Restore(reason);
            }
            catch (Exception ex)
            {
                Log.Exception("安全恢复失败", ex);
            }
        }

        public void OnWakeMessage(int source)
        {
            if (State == BlackoutState.Dark || State == BlackoutState.Arming)
            {
                string reason = "外部唤醒消息";
                if (source == 0) reason = "鼠标点击恢复";
                if (source == 1) reason = "鼠标滚轮恢复";
                if (source == 2) reason = "键盘按键恢复";
                Restore(reason);
            }
        }

        public void OnOverlayInput(InputSource source, bool isDown)
        {
            if (State == BlackoutState.Arming)
            {
                NoteArmingActivity();
                return;
            }

            if (State != BlackoutState.Dark)
            {
                return;
            }

            if (!isDown)
            {
                return;
            }

            if (source == InputSource.MouseWheel && !_settings.AllowWheelWake)
            {
                return;
            }

            RequestWake(source);
        }

        public void OnDisplayChange()
        {
            BlackoutState state = State;
            if (state != BlackoutState.Arming && state != BlackoutState.Dark)
            {
                return;
            }

            try
            {
                RefreshDisplays();
            }
            catch (Exception ex)
            {
                Log.Exception("显示拓扑变化处理失败，恢复显示", ex);
                Restore("显示拓扑变化失败");
            }
        }

        public void OnSessionChange(int reason)
        {
            if (reason == Native.WTS_SESSION_UNLOCK)
            {
                // A lock can happen while the physical display is off. The
                // audio endpoint may have been unavailable during the earlier
                // restore; retry after the user unlocks.
                try { _audio.RetryPendingRestore(); } catch (Exception ex) { Log.Exception("解锁后恢复音频失败", ex); }
                return;
            }

            if (reason == Native.WTS_SESSION_LOCK ||
                reason == Native.WTS_SESSION_LOGOFF ||
                reason == Native.WTS_CONSOLE_DISCONNECT ||
                reason == Native.WTS_REMOTE_DISCONNECT)
            {
                SafeRestore("会话状态变化 (" + reason + ")");
            }
        }

        public void OnPowerBroadcast(int wParam)
        {
            if (wParam == Native.PBT_APMSUSPEND)
            {
                SafeRestore("系统挂起");
            }
            else if (wParam == Native.PBT_APMRESUMEAUTOMATIC)
            {
                Interlocked.Exchange(ref _heartbeatTicks, DateTime.UtcNow.Ticks);
                try { _audio.RetryPendingRestore(); } catch (Exception ex) { Log.Exception("系统恢复后恢复音频失败", ex); }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _watchdogStop = true;
            SafeRestore("程序退出");
            try
            {
                _topmostGuard.Dispose();
            }
            catch
            {
            }
            try
            {
                _timer.Dispose();
            }
            catch
            {
            }
            try
            {
                if (_watchdog.IsAlive)
                {
                    _watchdog.Join(1000);
                }
            }
            catch
            {
            }
        }

        private bool ShouldDimExternalBacklight()
        {
            return !_suppressMonitorPower &&
                   _settings.TryPhysicalOff &&
                   SystemPower.HasBattery &&
                   !_settings.LaptopPhysicalOff &&
                   _settings.DimExternalBacklight;
        }

        private void TimerTick(object sender, EventArgs e)
        {
            Interlocked.Exchange(ref _heartbeatTicks, DateTime.UtcNow.Ticks);

            if (State == BlackoutState.Arming)
            {
                if (!_screenOffSent && (DateTime.UtcNow - _startedAt).TotalMilliseconds >= 400)
                {
                    _screenOffSent = true;
                    if (_suppressMonitorPower)
                    {
                        Log.Info("测试模式：跳过物理熄屏");
                    }
                    else if (!_settings.TryPhysicalOff)
                    {
                        Log.Info("物理关屏已关闭（用户设置）");
                    }
                    else if (SystemPower.HasBattery && !_settings.LaptopPhysicalOff)
                    {
                        if (_settings.DimExternalBacklight)
                        {
                            Log.Info("外接屏背光已关闭：{0} 台（显示器保持供电/输入可用）", _backlightStates.Count);
                        }
                        else
                        {
                            Log.Info("检测到电池：跳过物理关屏，仅保持全屏黑层（外接背光关闭已关闭）");
                        }
                    }
                    else
                    {
                        _physicalOffRequested = MonitorPower.TrySet(false);
                        Log.Info("物理熄屏请求：success={0}（battery={1}, laptopPhysicalOff={2}）",
                            _physicalOffRequested, SystemPower.HasBattery, _settings.LaptopPhysicalOff);
                        if (_physicalOffRequested)
                        {
                            // Reasserting topmost windows every 150 ms can wake
                            // a laptop panel and fight DPMS. Once the display is
                            // physically off, pause the guard until restore.
                            try { _topmostGuard.Stop(); } catch { }
                        }
                    }
                }

                if (DateTime.UtcNow >= _armAt)
                {
                    bool changed = false;
                    lock (_sync)
                    {
                        if (_stateValue == (int)BlackoutState.Arming)
                        {
                            _stateValue = (int)BlackoutState.Dark;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        RaiseState(BlackoutState.Dark);
                        Log.Info("进入 Dark，等待鼠标点击或键盘按键");
                    }
                }
            }

            if (State == BlackoutState.Arming || State == BlackoutState.Dark)
            {
                MaybeRefreshExecutionState();
                // Audio refresh is intentionally not performed on the UI
                // thread here: Core Audio calls can delay low-level input hook
                // callbacks. The service refreshes on its own background worker.
                if (State == BlackoutState.Dark)
                {
                    _audio.RequestRefresh();
                }
            }
        }

        private void MaybeRefreshExecutionState()
        {
            if (!_settings.PreventSleep || _physicalOffRequested) return;
            if ((DateTime.UtcNow - _lastExecutionStateRefreshUtc).TotalSeconds < 30) return;

            // SetThreadExecutionState is per-thread. Keep the UI thread's
            // execution state asserted while the black layer is active so the
            // idle/lock timer is less likely to roll over.
            uint flags = Native.ES_CONTINUOUS | Native.ES_SYSTEM_REQUIRED | Native.ES_DISPLAY_REQUIRED;
            if (Native.SetThreadExecutionState(flags) == 0)
            {
                Log.Warn("刷新 SetThreadExecutionState 失败，Win32={0}", Marshal.GetLastWin32Error());
            }
            _lastExecutionStateRefreshUtc = DateTime.UtcNow;
        }

        private void NoteArmingActivity()
        {
            lock (_sync)
            {
                if (_stateValue == (int)BlackoutState.Arming)
                {
                    _armAt = DateTime.UtcNow.AddMilliseconds(700);
                }
            }
        }

        private void RequestWake(InputSource source)
        {
            lock (_sync)
            {
                if (_stateValue != (int)BlackoutState.Dark || _wakePending)
                {
                    return;
                }
                _wakePending = true;
            }

            int code = source == InputSource.Keyboard ? 2 : (source == InputSource.MouseWheel ? 1 : 0);
            bool posted = false;
            try
            {
                if (_dispatcher.IsHandleCreated)
                {
                    posted = Native.PostMessage(_dispatcher.Handle, (uint)Native.WM_APP_WAKE, new IntPtr(code), IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("投递唤醒消息失败", ex);
            }

            if (!posted)
            {
                Log.Warn("PostMessage 唤醒失败，尝试直接恢复");
                try
                {
                    if (_dispatcher.IsHandleCreated)
                    {
                        _dispatcher.BeginInvoke(new Action<int>(OnWakeMessage), code);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("BeginInvoke 唤醒失败", ex);
                }
                Restore("唤醒消息投递失败");
            }
        }

        private void InstallHooks()
        {
            IntPtr module = Native.GetModuleHandle(null);
            _mouseProc = MouseHookProc;
            _keyProc = KeyboardHookProc;

            _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, module, 0);
            if (_mouseHook == IntPtr.Zero)
            {
                Log.Warn("安装 WH_MOUSE_LL 失败，Win32={0}", Marshal.GetLastWin32Error());
            }

            _keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _keyProc, module, 0);
            if (_keyHook == IntPtr.Zero)
            {
                Log.Warn("安装 WH_KEYBOARD_LL 失败，Win32={0}", Marshal.GetLastWin32Error());
            }

            if (_mouseHook == IntPtr.Zero && _keyHook == IntPtr.Zero)
            {
                Log.Warn("低级输入钩子全部失败，使用覆盖窗口事件作为后备");
            }
        }

        private void UninstallHooks()
        {
            try
            {
                if (_mouseHook != IntPtr.Zero)
                {
                    Native.UnhookWindowsHookEx(_mouseHook);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("卸载鼠标钩子失败", ex);
            }
            finally
            {
                _mouseHook = IntPtr.Zero;
            }

            try
            {
                if (_keyHook != IntPtr.Zero)
                {
                    Native.UnhookWindowsHookEx(_keyHook);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("卸载键盘钩子失败", ex);
            }
            finally
            {
                _keyHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            try
            {
                int msg = wParam.ToInt32();
                if (State == BlackoutState.Arming)
                {
                    if (Native.IsMouseButtonDownMessage(msg) ||
                        Native.IsMouseButtonUpMessage(msg) ||
                        Native.IsMouseWheelMessage(msg))
                    {
                        NoteArmingActivity();
                    }
                    return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                }

                if (State == BlackoutState.Dark)
                {
                    if (Native.IsMouseButtonDownMessage(msg))
                    {
                        RequestWake(InputSource.MouseButton);
                        return new IntPtr(1);
                    }
                    if (Native.IsMouseWheelMessage(msg) && _settings.AllowWheelWake)
                    {
                        RequestWake(InputSource.MouseWheel);
                        return new IntPtr(1);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("鼠标钩子回调异常", ex);
            }

            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            try
            {
                int msg = wParam.ToInt32();
                if (State == BlackoutState.Arming)
                {
                    if (Native.IsKeyboardMessage(msg) || Native.IsKeyboardUpMessage(msg))
                    {
                        NoteArmingActivity();
                    }
                    return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                }

                if (State == BlackoutState.Dark && Native.IsKeyboardMessage(msg))
                {
                    RequestWake(InputSource.Keyboard);
                    return new IntPtr(1);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("键盘钩子回调异常", ex);
            }

            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void RefreshDisplays()
        {
            List<DisplayInfo> displays = DisplayManager.Enumerate();
            if (displays.Count == 0)
            {
                Log.Warn("显示拓扑变化后无显示器，恢复显示");
                Restore("无活动显示器");
                return;
            }

            bool failed = false;
            List<OverlayForm> current = new List<OverlayForm>();
            Dictionary<string, OverlayForm> newMap = new Dictionary<string, OverlayForm>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < displays.Count; i++)
            {
                DisplayInfo info = displays[i];
                string key = GetDisplayKey(info);
                OverlayForm form;
                if (_overlayByKey.TryGetValue(key, out form))
                {
                    Rectangle bounds = new Rectangle(info.Bounds.Left, info.Bounds.Top, info.Bounds.Width, info.Bounds.Height);
                    if (form.Bounds != bounds)
                    {
                        form.Bounds = bounds;
                    }
                    Native.SetWindowPos(
                        form.Handle,
                        Native.HWND_TOPMOST,
                        info.Bounds.Left,
                        info.Bounds.Top,
                        info.Bounds.Width,
                        info.Bounds.Height,
                        Native.SWP_SHOWWINDOW);
                    current.Add(form);
                    newMap[key] = form;
                }
                else
                {
                    try
                    {
                        OverlayForm added = new OverlayForm(this, info, _cursor);
                        added.Show();
                        current.Add(added);
                        newMap[key] = added;
                        Log.Info("为新增显示器创建覆盖层：{0} {1}x{2}", info.DeviceName, info.Bounds.Width, info.Bounds.Height);
                    }
                    catch (Exception ex)
                    {
                        failed = true;
                        Log.Exception("为新增显示器创建覆盖层失败", ex);
                    }
                }
            }

            List<OverlayForm> stale = new List<OverlayForm>();
            for (int i = 0; i < _overlays.Count; i++)
            {
                if (!current.Contains(_overlays[i]))
                {
                    stale.Add(_overlays[i]);
                }
            }

            lock (_sync)
            {
                _overlays.Clear();
                _overlays.AddRange(current);
                _overlayByKey.Clear();
                foreach (KeyValuePair<string, OverlayForm> pair in newMap)
                {
                    _overlayByKey[pair.Key] = pair.Value;
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                try { stale[i].Close(); stale[i].Dispose(); } catch { }
            }

            for (int i = 0; i < current.Count; i++)
            {
                try { Native.SetWindowPos(current[i].Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE); }
                catch { }
            }
            Native.DwmFlush();

            _topmostGuard.SetTargets(GetOverlayHandles());

            if (failed)
            {
                Restore("热插拔显示器覆盖失败");
            }
            else if (current.Count > 0)
            {
                current[0].FocusForInput();
            }
        }

        private List<IntPtr> GetOverlayHandles()
        {
            List<IntPtr> handles = new List<IntPtr>();
            for (int i = 0; i < _overlays.Count; i++)
            {
                try
                {
                    if (_overlays[i].IsHandleCreated) handles.Add(_overlays[i].Handle);
                }
                catch
                {
                }
            }
            return handles;
        }

        private void FocusPrimaryOverlay()
        {
            OverlayForm primary = null;
            for (int i = 0; i < _overlays.Count; i++)
            {
                if (_overlays[i].Display.Primary)
                {
                    primary = _overlays[i];
                    break;
                }
            }
            if (primary == null && _overlays.Count > 0)
            {
                primary = _overlays[0];
            }
            if (primary != null)
            {
                primary.FocusForInput();
            }
        }

        private static string GetDisplayKey(DisplayInfo info)
        {
            return (info.DeviceName ?? string.Empty) + "|" + info.Bounds.Left + "|" + info.Bounds.Top + "|" + info.Bounds.Width + "|" + info.Bounds.Height;
        }

        private void RaiseState(BlackoutState state)
        {
            Action<BlackoutState> handler = StateChanged;
            if (handler != null)
            {
                try { handler(state); } catch (Exception ex) { Log.Exception("StateChanged 回调异常", ex); }
            }
        }

        private void Fail(string message)
        {
            lock (_sync)
            {
                _lastError = message;
            }
            Log.Error(message);
            Action<string> handler = Error;
            if (handler != null)
            {
                try { handler(message); } catch (Exception ex) { Log.Exception("Error 回调异常", ex); }
            }
        }

        private void WatchdogLoop()
        {
            while (!_watchdogStop)
            {
                try
                {
                    Thread.Sleep(1000);
                    BlackoutState state = State;
                    if (state == BlackoutState.Idle)
                    {
                        continue;
                    }

                    long ticks = Interlocked.Read(ref _heartbeatTicks);
                    if (ticks > 0 && (DateTime.UtcNow.Ticks - ticks) > TimeSpan.TicksPerSecond * 10)
                    {
                        Log.Error("UI 心跳超过 10 秒未更新（状态 {0}），将终止进程以解除黑屏", state);
                        AudioMuteService.EmergencyRestoreFromDisk();
                        Native.TerminateProcess(Native.GetCurrentProcess(), 1);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("看门狗异常", ex);
                }
            }
        }
    }
}
