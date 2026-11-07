using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class MainForm : Form
    {
        private const int HotkeyId = 0x5343;

        private readonly Settings _settings;
        private readonly UpdateGuardClient _guard;
        private readonly BlackoutController _blackout;
        private readonly AppNotifier _notifier;
        private readonly TrayController _tray;

        private readonly StatusPill _statusPill;
        private readonly Button _settingsButton;
        private readonly Button _blackoutButton;
        private readonly Button _displayRefreshButton;
        private readonly Button _guardActionButton;
        private readonly Button _guardDetailsButton;
        private readonly Button _logsButton;
        private readonly Button _exitButton;
        private readonly Label _displayCountLabel;
        private readonly Label _guardSummaryLabel;
        private readonly Label _guardReasonLabel;
        private readonly Label _noticeLabel;
        private readonly Label _hintLabel;
        private readonly ToggleSwitch _wakeToggle;
        private readonly ToggleSwitch _wheelToggle;
        private readonly ToggleSwitch _muteToggle;

        private HotkeySpec _hotkey;
        private GuardState _guardState = GuardState.Off;
        private string _guardMessage = string.Empty;
        private bool _hotkeyRegistered;
        private bool _sessionRegistered;
        private bool _allowClose;
        private bool _exiting;
        private bool _updatingUi;
        private bool _minimizeBalloonShown;
        private string _pendingNotice;
        private bool _pendingNoticeIsError;

        public event EventHandler ExitRequested;

        public MainForm(Settings settings, UpdateGuardClient guard)
        {
            _settings = settings;
            _guard = guard;
            _notifier = new AppNotifier();
            _notifier.SetEnabled(_settings.ShowTrayNotifications);

            _blackout = new BlackoutController(this, _settings);
            _blackout.StateChanged += OnBlackoutStateChanged;
            _blackout.Error += OnBlackoutError;
            _guard.StatusChanged += OnGuardStatusChanged;

            Text = "幕帘 ScreenCurtain";
            Font = AppTheme.BodyFont;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 650);
            BackColor = AppTheme.WindowBackground;
            ShowInTaskbar = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label brand = AppTheme.CreateLabel("幕帘", AppTheme.TitleFont, AppTheme.TextPrimary);
            brand.SetBounds(20, 10, 300, 40);
            Controls.Add(brand);

            Label subtitle = AppTheme.CreateLabel(
                "一键关闭所有显示器 · 点击或按键恢复 · 熄屏时静音系统音频",
                AppTheme.SmallFont,
                AppTheme.TextSecondary);
            subtitle.SetBounds(22, 50, 500, 20);
            Controls.Add(subtitle);

            _statusPill = new StatusPill();
            _statusPill.SetBounds(438, 18, 180, 28);
            _statusPill.SetStatus("正在读取状态", AppTheme.TextSecondary, AppTheme.NeutralSoft, AppTheme.TextSecondary);
            Controls.Add(_statusPill);

            _settingsButton = UiLayout.SecondaryButton("设置");
            _settingsButton.SetBounds(628, 16, 72, 32);
            _settingsButton.Click += delegate { OpenSettings(0); };
            Controls.Add(_settingsButton);

            _noticeLabel = AppTheme.CreateLabel(string.Empty, AppTheme.SmallFont, AppTheme.Danger);
            _noticeLabel.SetBounds(22, 72, 675, 20);
            _noticeLabel.Visible = false;
            Controls.Add(_noticeLabel);

            CardPanel actionCard = new CardPanel();
            actionCard.SetBounds(20, 94, 680, 118);
            Controls.Add(actionCard);

            Label actionTitle = AppTheme.CreateLabel("立即熄屏", new Font(AppTheme.BodyFont.FontFamily, 15F, FontStyle.Bold), AppTheme.TextPrimary);
            actionTitle.SetBounds(18, 16, 300, 32);
            actionCard.Controls.Add(actionTitle);

            Label actionSubtitle = AppTheme.CreateLabel(
                "覆盖所有显示器 · 鼠标移动不会退出 · 点击鼠标或按键盘恢复",
                AppTheme.SmallFont,
                AppTheme.TextSecondary);
            actionSubtitle.SetBounds(20, 48, 430, 22);
            actionCard.Controls.Add(actionSubtitle);

            _blackoutButton = UiLayout.PrimaryButton("立即熄屏");
            _blackoutButton.SetBounds(470, 34, 190, 50);
            _blackoutButton.Font = new Font(AppTheme.BodyFont.FontFamily, 11F, FontStyle.Bold);
            _blackoutButton.Click += delegate { EnterBlackout(); };
            actionCard.Controls.Add(_blackoutButton);

            Label actionHint = AppTheme.CreateLabel("快捷键会显示在按钮旁边", AppTheme.SmallFont, AppTheme.TextSecondary);
            actionHint.SetBounds(20, 74, 430, 38);
            actionCard.Controls.Add(actionHint);
            _hintLabel = actionHint;

            CardPanel displayCard = new CardPanel();
            displayCard.SetBounds(20, 222, 680, 64);
            Controls.Add(displayCard);

            Label displayTitle = AppTheme.CreateLabel("显示器", AppTheme.SectionFont, AppTheme.TextPrimary);
            displayTitle.SetBounds(16, 10, 80, 24);
            displayCard.Controls.Add(displayTitle);

            _displayCountLabel = AppTheme.CreateLabel("正在枚举…", AppTheme.BodyFont, AppTheme.TextSecondary);
            _displayCountLabel.SetBounds(100, 12, 430, 22);
            displayCard.Controls.Add(_displayCountLabel);

            _displayRefreshButton = new Button();
            AppTheme.StyleLink(_displayRefreshButton);
            _displayRefreshButton.Text = "刷新";
            _displayRefreshButton.SetBounds(586, 10, 70, 24);
            _displayRefreshButton.Click += delegate { UpdateDisplayCount(); };
            displayCard.Controls.Add(_displayRefreshButton);

            Label displayHint = AppTheme.CreateLabel("每台显示器使用独立无边框全屏层；点击或按键后恢复。", AppTheme.SmallFont, AppTheme.TextSecondary);
            displayHint.SetBounds(16, 36, 630, 18);
            displayCard.Controls.Add(displayHint);

            CardPanel guardCard = new CardPanel();
            guardCard.SetBounds(20, 296, 680, 128);
            Controls.Add(guardCard);

            Label guardTitle = AppTheme.CreateLabel("更新重启保护", AppTheme.SectionFont, AppTheme.TextPrimary);
            guardTitle.SetBounds(16, 10, 200, 24);
            guardCard.Controls.Add(guardTitle);

            _guardSummaryLabel = AppTheme.CreateLabel("未启用", AppTheme.BodyFont, AppTheme.TextSecondary);
            _guardSummaryLabel.SetBounds(16, 38, 480, 22);
            guardCard.Controls.Add(_guardSummaryLabel);

            _guardReasonLabel = AppTheme.CreateLabel("不修改 Windows Update 设置。", AppTheme.SmallFont, AppTheme.TextSecondary);
            _guardReasonLabel.SetBounds(16, 62, 470, 36);
            guardCard.Controls.Add(_guardReasonLabel);

            _guardActionButton = UiLayout.SecondaryButton("启用保护…");
            _guardActionButton.SetBounds(520, 12, 140, 34);
            _guardActionButton.Click += delegate { OnGuardAction(); };
            guardCard.Controls.Add(_guardActionButton);

            _guardDetailsButton = new Button();
            AppTheme.StyleLink(_guardDetailsButton);
            _guardDetailsButton.Text = "详情";
            _guardDetailsButton.SetBounds(576, 58, 80, 26);
            _guardDetailsButton.Click += delegate { OpenSettings(2); };
            guardCard.Controls.Add(_guardDetailsButton);

            Label guardFootnote = AppTheme.CreateLabel("仅在本程序运行期间临时暂停自动更新；退出时恢复。", AppTheme.SmallFont, AppTheme.TextSecondary);
            guardFootnote.SetBounds(16, 100, 630, 18);
            guardCard.Controls.Add(guardFootnote);

            CardPanel optionsCard = new CardPanel();
            optionsCard.SetBounds(20, 434, 680, 90);
            Controls.Add(optionsCard);

            Label optionsTitle = AppTheme.CreateLabel("快速选项", AppTheme.SectionFont, AppTheme.TextPrimary);
            optionsTitle.SetBounds(16, 8, 150, 24);
            optionsCard.Controls.Add(optionsTitle);

            _wakeToggle = new ToggleSwitch();
            _wakeToggle.SetBounds(179, 40, 42, 24);
            _wakeToggle.Checked = _settings.PreventSleep;
            _wakeToggle.CheckedChanged += delegate { OnToggleChanged(); };
            optionsCard.Controls.Add(_wakeToggle);
            Label wakeLabel = AppTheme.CreateLabel("保持系统唤醒", AppTheme.BodyFont, AppTheme.TextPrimary);
            wakeLabel.SetBounds(16, 40, 150, 24);
            optionsCard.Controls.Add(wakeLabel);

            _wheelToggle = new ToggleSwitch();
            _wheelToggle.SetBounds(400, 40, 42, 24);
            _wheelToggle.Checked = _settings.AllowWheelWake;
            _wheelToggle.CheckedChanged += delegate { OnToggleChanged(); };
            optionsCard.Controls.Add(_wheelToggle);
            Label wheelLabel = AppTheme.CreateLabel("滚轮也可恢复", AppTheme.BodyFont, AppTheme.TextPrimary);
            wheelLabel.SetBounds(237, 40, 155, 24);
            optionsCard.Controls.Add(wheelLabel);

            _muteToggle = new ToggleSwitch();
            _muteToggle.SetBounds(621, 40, 42, 24);
            _muteToggle.Checked = _settings.MuteSystemAudio;
            _muteToggle.CheckedChanged += delegate { OnToggleChanged(); };
            optionsCard.Controls.Add(_muteToggle);
            Label muteLabel = AppTheme.CreateLabel("熄屏时静音", AppTheme.BodyFont, AppTheme.TextPrimary);
            muteLabel.SetBounds(458, 40, 155, 24);
            optionsCard.Controls.Add(muteLabel);

            _logsButton = new Button();
            AppTheme.StyleLink(_logsButton);
            _logsButton.Text = "查看日志";
            _logsButton.SetBounds(16, 540, 92, 28);
            _logsButton.Click += delegate { OpenLogs(); };
            Controls.Add(_logsButton);

            _exitButton = new Button();
            AppTheme.StyleLink(_exitButton);
            _exitButton.Text = "退出并恢复系统设置…";
            _exitButton.SetBounds(510, 540, 196, 28);
            _exitButton.ForeColor = AppTheme.Danger;
            _exitButton.Click += delegate { RequestExit(); };
            Controls.Add(_exitButton);

            Label hint = AppTheme.CreateLabel(
                "保护期间关闭窗口只会最小化到任务栏。",
                AppTheme.SmallFont,
                AppTheme.TextSecondary);
            hint.SetBounds(110, 544, 380, 30);
            Controls.Add(hint);

            Label version = AppTheme.CreateLabel(
                "ScreenCurtain " + ReleaseInfo.Version +
                (_settings.FirstRun ? "" : (AppPaths.IsPortable
                    ? Localization.T(" · 便携模式")
                    : Localization.T(" · 安装版"))),
                AppTheme.SmallFont,
                Color.FromArgb(130, 130, 130));
            version.SetBounds(20, 594, 400, 18);
            Controls.Add(version);

            _tray = new TrayController(_notifier);
            _tray.OpenRequested += delegate { ShowMainWindow(); };
            _tray.BlackoutRequested += delegate { EnterBlackout(); };
            _tray.RestoreRequested += delegate { _blackout.SafeRestore("托盘恢复"); };
            _tray.RepairRequested += delegate { StartGuard(); };
            _tray.SettingsRequested += delegate { OpenSettings(0); };
            _tray.ExitRequested += delegate { RequestExit(); };
            _tray.SetHotkeyText(_settings.GetHotkey().Text);
            _tray.SetDisplayCount(DisplayManager.Enumerate().Count);
            _tray.SetGuardState(_guard.State, _guard.Message);

            _hotkey = _settings.GetHotkey();
            UpdateDisplayCount();
            UpdateGuardUi(_guard.State, _guard.Message);
            UpdateStatusPill();
            UpdateToggles();
            UpdateHotkeyText();

            Shown += delegate
            {
                UpdateDisplayCount();
                if (_settings.StartMinimized)
                {
                    WindowState = FormWindowState.Minimized;
                }
                if (_guard.State == GuardState.Off && _settings.GuardEnabled)
                {
                    StartGuard();
                }
                else if (_guard.State == GuardState.Active || _guard.State == GuardState.Degraded)
                {
                    EnableShutdownBlock(true);
                    UpdateGuardUi(_guard.State, _guard.Message);
                }
                if (HasCommandLine("--open-settings"))
                {
                    BeginInvoke(new Action(delegate { OpenSettings(GetSettingsPageArgument()); }));
                }
            };
            Localization.Apply(this);
            FormClosing += OnFormClosing;
            FormClosed += delegate { Cleanup(); };
            Load += delegate { UiDpi.Apply(this); };
        }

        public GuardState GuardStateValue
        {
            get { return _guardState; }
        }

        public Settings Settings
        {
            get { return _settings; }
        }

        public void ShowMainWindow()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Show();
            Activate();
            BringToFront();
        }

        public void PrepareExit()
        {
            _allowClose = true;
            try { _blackout.SafeRestore("退出程序"); } catch { }
            try { _blackout.Dispose(); } catch { }
            try { ShutdownGuard.Active = false; ShutdownGuard.Clear(Handle); } catch { }
            try { if (_hotkeyRegistered) Native.UnregisterHotKey(Handle, HotkeyId); } catch { }
            try { if (_sessionRegistered) Native.WTSUnRegisterSessionNotification(Handle); } catch { }
            try { _guard.StatusChanged -= OnGuardStatusChanged; } catch { }
            _hotkeyRegistered = false;
            _sessionRegistered = false;
            _tray.Dispose();
        }

        public bool TryChangeHotkey(HotkeySpec spec, out string error)
        {
            error = null;
            if (spec == null)
            {
                error = "无效的快捷键。";
                return false;
            }
            if (!IsHandleCreated)
            {
                error = "主窗口尚未准备好，请稍后再试。";
                return false;
            }

            HotkeySpec old = _hotkey;
            if (_hotkeyRegistered)
            {
                Native.UnregisterHotKey(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }
            bool ok = Native.RegisterHotKey(Handle, HotkeyId, spec.Modifiers, spec.VirtualKey);
            if (!ok)
            {
                int code = Marshal.GetLastWin32Error();
                error = "快捷键 " + spec.Text + " 注册失败（可能被其他程序占用，Win32=" + code + "）。";
                if (old != null)
                {
                    _hotkeyRegistered = Native.RegisterHotKey(Handle, HotkeyId, old.Modifiers, old.VirtualKey);
                }
                return false;
            }

            _hotkeyRegistered = true;
            _hotkey = spec;
            _settings.Hotkey = spec.Text;
            _settings.Save();
            UpdateHotkeyText();
            return true;
        }

        public void SetGuardEnabled(bool enabled)
        {
            if (enabled) StartGuard();
            else StopGuard();
        }

        public void PreviewBlackout(int seconds)
        {
            PreviewOverlay.Show(this, seconds, _settings);
        }

        public void OpenSettings(int page)
        {
            if (_blackout.State != BlackoutState.Idle)
            {
                return;
            }
            using (SettingsForm form = new SettingsForm(_settings, _guard, this, page))
            {
                form.ShowDialog(this);
            }
            RefreshFromSettings();
        }

        public void RefreshFromSettings()
        {
            _notifier.SetEnabled(_settings.ShowTrayNotifications);
            UpdateToggles();
            UpdateHotkeyText();
            _tray.SetHotkeyText(_settings.GetHotkey().Text);
            UpdateDisplayCount();
        }

        private void UpdateToggles()
        {
            _updatingUi = true;
            _wakeToggle.Checked = _settings.PreventSleep;
            _wheelToggle.Checked = _settings.AllowWheelWake;
            _muteToggle.Checked = _settings.MuteSystemAudio;
            _updatingUi = false;
        }

        private void OnToggleChanged()
        {
            if (_updatingUi) return;
            _settings.PreventSleep = _wakeToggle.Checked;
            _settings.AllowWheelWake = _wheelToggle.Checked;
            _settings.MuteSystemAudio = _muteToggle.Checked;
            _settings.Save();
        }

        private void UpdateHotkeyText()
        {
            HotkeySpec spec = _settings.GetHotkey();
            _blackoutButton.Text = Localization.T("立即熄屏");
            _hintLabel.Text = Localization.IsEnglish
                ? "Shortcut " + spec.Text + " · Tray menu can also black out"
                : "快捷键 " + spec.Text + " · 托盘菜单也可熄屏";
            _tray.SetHotkeyText(spec.Text);
        }

        private void UpdateStatusPill()
        {
            if (_blackout.State != BlackoutState.Idle)
            {
                _statusPill.SetStatus(Localization.T("黑屏中 · 点击或按键恢复"), AppTheme.Accent, Color.FromArgb(229, 241, 251), AppTheme.Accent);
                return;
            }
            if (_guardState == GuardState.Active)
            {
                _statusPill.SetStatus(Localization.T("保护已开启"), AppTheme.Success, AppTheme.SuccessSoft, AppTheme.Success);
            }
            else if (_guardState == GuardState.Starting)
            {
                _statusPill.SetStatus(Localization.T("保护启动中"), AppTheme.Warning, AppTheme.WarningSoft, AppTheme.Warning);
            }
            else if (_guardState == GuardState.Degraded)
            {
                _statusPill.SetStatus(Localization.T("保护降级"), AppTheme.Warning, AppTheme.WarningSoft, AppTheme.Warning);
            }
            else if (_guardState == GuardState.Stopping)
            {
                _statusPill.SetStatus(Localization.T("正在恢复设置"), AppTheme.Warning, AppTheme.WarningSoft, AppTheme.Warning);
            }
            else
            {
                _statusPill.SetStatus(Localization.T("保护未开启"), Color.FromArgb(139, 148, 158), AppTheme.NeutralSoft, AppTheme.TextSecondary);
            }
        }

        private void UpdateGuardUi(GuardState state, string message)
        {
            if (IsDisposed || Disposing) return;
            _guardState = state;
            _guardMessage = message ?? string.Empty;

            string summary;
            string reason;
            string action;
            bool actionEnabled = true;

            switch (state)
            {
                case GuardState.Active:
                    summary = "已开启 · 正在阻止 Windows 更新重启";
                    reason = string.IsNullOrEmpty(message) ? "退出幕帘或关闭保护时恢复 Windows Update 原始设置。" : message;
                    action = "关闭保护";
                    break;
                case GuardState.Starting:
                    summary = "正在请求管理员权限…";
                    reason = "UAC 请求可能出现在桌面上；取消不会导致程序退出，但保护会降级。";
                    action = "启动中…";
                    actionEnabled = false;
                    break;
                case GuardState.Stopping:
                    summary = "正在恢复 Windows Update 原始设置…";
                    reason = "请等待策略恢复完成。";
                    action = "正在恢复…";
                    actionEnabled = false;
                    break;
                case GuardState.Degraded:
                    summary = "保护未生效 · 更新设置需要修复";
                    reason = string.IsNullOrEmpty(message) ? "可以重试，或查看日志。" : message;
                    action = "重试";
                    break;
                default:
                    summary = "未启用 · 不修改系统更新设置";
                    reason = "可在需要长时间运行时启用；启用时会请求管理员权限。";
                    action = "启用保护…";
                    break;
            }

            _guardSummaryLabel.Text = Localization.T(summary);
            _guardReasonLabel.Text = Localization.T(reason);
            _guardActionButton.Text = Localization.T(action);
            _guardActionButton.Enabled = actionEnabled;
            _guardActionButton.ForeColor = state == GuardState.Active ? AppTheme.TextPrimary : AppTheme.Accent;
            _guardActionButton.BackColor = state == GuardState.Active ? AppTheme.Surface : Color.FromArgb(235, 245, 255);
            _guardActionButton.FlatAppearance.BorderColor = state == GuardState.Active ? AppTheme.Border : Color.FromArgb(180, 215, 245);
            _guardDetailsButton.Visible = true;
            UpdateStatusPill();
            _tray.SetGuardState(state, message);
            _tray.SetDisplayCount(DisplayManager.Enumerate().Count);
        }

        private void OnGuardStatusChanged(object sender, GuardStatusEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<GuardStatusEventArgs>(ApplyGuardStatus), e);
                    return;
                }
                ApplyGuardStatus(e);
            }
            catch (Exception ex)
            {
                Log.Exception("更新保护状态回调失败", ex);
            }
        }

        private void ApplyGuardStatus(GuardStatusEventArgs e)
        {
            if (e == null) return;
            if (e.State == GuardState.Active || e.State == GuardState.Degraded)
            {
                EnableShutdownBlock(true);
            }
            else if (e.State == GuardState.Off)
            {
                EnableShutdownBlock(false);
            }

            UpdateGuardUi(e.State, e.Message);
            if (e.State == GuardState.Degraded)
            {
                _notifier.Warning("更新保护降级", "更新设置未能完整生效，请打开设置重试修复。");
            }
        }

        private void OnGuardAction()
        {
            if (_guardState == GuardState.Active)
            {
                bool confirmed = ConfirmDialog.Show(
                    this,
                    "关闭更新重启保护",
                    "关闭后幕帘将恢复 Windows Update 原始设置，运行期间的自动更新重启保护会停止。熄屏功能仍然可用。",
                    "关闭保护",
                    false);
                if (confirmed) StopGuard();
                return;
            }
            if (_guardState == GuardState.Degraded || _guardState == GuardState.Off)
            {
                StartGuard();
            }
        }

        private void StartGuard()
        {
            if (_guardState == GuardState.Starting || _guardState == GuardState.Active || _guardState == GuardState.Stopping)
            {
                return;
            }
            _settings.GuardEnabled = true;
            _settings.Save();
            EnableShutdownBlock(true);
            UpdateGuardUi(GuardState.Starting, "正在请求管理员权限并写入临时策略…");
            _guard.Start();
        }

        private void StopGuard()
        {
            _settings.GuardEnabled = false;
            _settings.Save();
            try { _guard.Stop(); }
            catch (Exception ex) { Log.Exception("停止更新保护失败", ex); }
            EnableShutdownBlock(false);
            if (_guard.State == GuardState.Degraded)
            {
                UpdateGuardUi(GuardState.Degraded, "更新设置恢复失败，请重试修复。");
            }
            else
            {
                UpdateGuardUi(GuardState.Off, "已关闭");
            }
        }

        private void EnableShutdownBlock(bool enable)
        {
            ShutdownGuard.Active = enable;
            try
            {
                if (!IsHandleCreated) return;
                if (enable)
                {
                    bool ok = Native.ShutdownBlockReasonCreate(Handle, ShutdownGuard.Reason);
                    if (!ok)
                    {
                        Log.Warn("注册关机阻止原因失败，Win32={0}", Marshal.GetLastWin32Error());
                    }
                }
                else
                {
                    Native.ShutdownBlockReasonDestroy(Handle);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("设置关机阻止状态失败", ex);
            }
        }

        private void EnterBlackout()
        {
            if (_blackout.State != BlackoutState.Idle) return;
            if (!_blackout.Enter())
            {
                string error = _blackout.LastError ?? "无法进入熄屏状态";
                ShowNotice(error, true);
                _notifier.Error("无法进入熄屏", error);
            }
        }

        private void OnBlackoutStateChanged(BlackoutState state)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<BlackoutState>(OnBlackoutStateChanged), state);
                    return;
                }
            }
            catch { return; }

            _tray.SetBlackoutState(state);
            _blackoutButton.Enabled = state == BlackoutState.Idle;
            _settingsButton.Enabled = state == BlackoutState.Idle;
            _notifier.SetDark(state != BlackoutState.Idle);
            UpdateStatusPill();

            if (state == BlackoutState.Idle)
            {
                if (_pendingNotice != null)
                {
                    ShowNotice(_pendingNotice, _pendingNoticeIsError);
                    _pendingNotice = null;
                }
                Activate();
            }
        }

        private void OnBlackoutError(string message)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string>(OnBlackoutError), message);
                    return;
                }
            }
            catch { return; }

            if (_blackout.State != BlackoutState.Idle || _notifier.IsDark)
            {
                _pendingNotice = message;
                _pendingNoticeIsError = true;
                return;
            }
            ShowNotice(message, true);
            _notifier.Error("熄屏异常", message);
        }

        private void ShowNotice(string message, bool isError)
        {
            _noticeLabel.Text = message ?? string.Empty;
            _noticeLabel.ForeColor = isError ? AppTheme.Danger : AppTheme.TextSecondary;
            _noticeLabel.Visible = !string.IsNullOrEmpty(_noticeLabel.Text);
        }

        private static bool HasCommandLine(string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static int GetSettingsPageArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--settings-page=", StringComparison.OrdinalIgnoreCase))
                {
                    int page;
                    if (int.TryParse(args[i].Substring("--settings-page=".Length), out page)) return page;
                }
            }
            return 0;
        }

        private void UpdateDisplayCount()
        {            int count;
            try { count = DisplayManager.Enumerate().Count; }
            catch { count = 0; }
            _displayCountLabel.Text = Localization.IsEnglish
                ? count + " display(s) · " + (count > 0 ? Localization.T("覆盖就绪") : Localization.T("未检测到显示器"))
                : count + " 台显示器 · " + (count > 0 ? "覆盖就绪" : "未检测到显示器");
            _displayCountLabel.ForeColor = count > 0 ? AppTheme.TextPrimary : AppTheme.Warning;
            _tray.SetDisplayCount(count);
        }

        private void OpenLogs()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Log.DirectoryPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowNotice("无法打开日志目录：" + ex.Message, true);
            }
        }

        private void RequestExit()
        {
            if (_exiting) return;
            string message = _guardState == GuardState.Active
                ? "退出幕帘会先恢复 Windows Update 原始设置，并停止更新重启保护。熄屏状态也会被解除。"
                : _guardState == GuardState.Degraded
                    ? "当前更新设置处于降级/未恢复状态。退出前请确认已经尝试修复，否则可能残留临时策略。"
                    : "退出幕帘后将无法继续阻止更新重启，但不会修改 Windows Update 设置。";
            if (_settings.ConfirmExit || _guardState == GuardState.Active || _guardState == GuardState.Degraded)
            {
                if (!ConfirmDialog.Show(this, "退出幕帘？", message, "退出并恢复", true))
                {
                    return;
                }
            }
            _exiting = true;
            _allowClose = true;
            EventHandler handler = ExitRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            if (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing)
            {
                _allowClose = true;
                ShutdownGuard.AllowShutdown = true;
                try { _blackout.SafeRestore("Windows 关闭"); } catch { }
                return;
            }

            if (_guardState == GuardState.Active || _guardState == GuardState.Degraded || _guardState == GuardState.Starting)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                if (!_minimizeBalloonShown && _settings.ShowTrayNotifications && !_notifier.IsDark)
                {
                    _minimizeBalloonShown = true;
                    _notifier.Info("幕帘仍在保护中", "窗口已最小化到任务栏；请使用“退出并恢复系统设置”停止保护。");
                }
                return;
            }

            if (_settings.CloseAction == CloseAction.Minimize)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                if (!_minimizeBalloonShown && _settings.ShowTrayNotifications && !_notifier.IsDark)
                {
                    _minimizeBalloonShown = true;
                    _notifier.Info("幕帘仍在运行", "窗口已最小化到任务栏；可从托盘或任务栏重新打开。");
                }
                return;
            }

            e.Cancel = true;
            RequestExit();
        }

        private void Cleanup()
        {
            try { _blackout.Dispose(); } catch { }
            try { _guard.StatusChanged -= OnGuardStatusChanged; } catch { }
            try { if (_hotkeyRegistered) Native.UnregisterHotKey(Handle, HotkeyId); } catch { }
            try { if (_sessionRegistered) Native.WTSUnRegisterSessionNotification(Handle); } catch { }
            try { ShutdownGuard.Active = false; ShutdownGuard.Clear(Handle); } catch { }
            try { _tray.Dispose(); } catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (IsDisposed) return;

            HotkeySpec spec = _settings.GetHotkey();
            _hotkey = spec;
            _hotkeyRegistered = Native.RegisterHotKey(Handle, HotkeyId, spec.Modifiers, spec.VirtualKey);
            if (!_hotkeyRegistered)
            {
                ShowNotice("全局热键 " + spec.Text + " 注册失败，可在设置中更换。", true);
                Log.Warn("注册全局热键失败：{0}，Win32={1}", spec.Text, Marshal.GetLastWin32Error());
            }

            _sessionRegistered = Native.WTSRegisterSessionNotification(Handle, 0);
            if (!_sessionRegistered)
            {
                Log.Warn("WTSRegisterSessionNotification 失败，Win32={0}", Marshal.GetLastWin32Error());
            }

            if (_guard.State == GuardState.Active || _guard.State == GuardState.Degraded)
            {
                EnableShutdownBlock(true);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_APP_WAKE)
            {
                _blackout.OnWakeMessage(m.WParam.ToInt32());
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                EnterBlackout();
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == Native.WM_QUERYENDSESSION)
            {
                uint flags = unchecked((uint)m.LParam.ToInt64());
                bool blocked = ShutdownGuard.HandleQueryEndSession(Handle, flags);
                m.Result = blocked ? IntPtr.Zero : new IntPtr(1);
                return;
            }

            if (m.Msg == Native.WM_ENDSESSION)
            {
                if (m.WParam != IntPtr.Zero)
                {
                    Log.Info("Windows 会话结束，执行清理");
                    _allowClose = true;
                    ShutdownGuard.AllowShutdown = true;
                    try { _blackout.SafeRestore("Windows 会话结束"); } catch { }
                }
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == Native.WM_DISPLAYCHANGE)
            {
                try { _blackout.OnDisplayChange(); } catch (Exception ex) { Log.Exception("处理显示变化失败", ex); }
                UpdateDisplayCount();
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == Native.WM_WTSSESSION_CHANGE)
            {
                try { _blackout.OnSessionChange(m.WParam.ToInt32()); } catch (Exception ex) { Log.Exception("处理会话变化失败", ex); }
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == Native.WM_POWERBROADCAST)
            {
                try { _blackout.OnPowerBroadcast(m.WParam.ToInt32()); } catch (Exception ex) { Log.Exception("处理电源事件失败", ex); }
                base.WndProc(ref m);
                return;
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _blackout.Dispose(); } catch { }
                try { _guard.StatusChanged -= OnGuardStatusChanged; } catch { }
                try { _tray.Dispose(); } catch { }
                try { if (_hotkeyRegistered) Native.UnregisterHotKey(Handle, HotkeyId); } catch { }
                try { if (_sessionRegistered) Native.WTSUnRegisterSessionNotification(Handle); } catch { }
                try { ShutdownGuard.Active = false; ShutdownGuard.Clear(Handle); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
