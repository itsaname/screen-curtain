using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class AppNotifier
    {
        private NotifyIcon _tray;
        private bool _dark;
        private bool _enabled = true;
        private string _pendingTitle;
        private string _pendingText;
        private ToolTipIcon _pendingIcon;

        public bool IsDark
        {
            get { return _dark; }
        }

        public void SetTray(NotifyIcon tray)
        {
            _tray = tray;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public void SetDark(bool dark)
        {
            _dark = dark;
            if (!dark)
            {
                FlushPending();
            }
        }

        public void Info(string title, string text)
        {
            Show(title, text, ToolTipIcon.Info);
        }

        public void Warning(string title, string text)
        {
            Show(title, text, ToolTipIcon.Warning);
        }

        public void Error(string title, string text)
        {
            Show(title, text, ToolTipIcon.Error);
        }

        private void Show(string title, string text, ToolTipIcon icon)
        {
            title = Localization.T(title);
            text = Localization.T(text);
            Log.Info("通知：{0} — {1}", title, text);
            if (_dark)
            {
                _pendingTitle = title;
                _pendingText = text;
                _pendingIcon = icon;
                return;
            }
            if (!_enabled || _tray == null)
            {
                return;
            }
            try
            {
                _tray.ShowBalloonTip(3000, title, text, icon);
            }
            catch
            {
            }
        }

        private void FlushPending()
        {
            if (string.IsNullOrEmpty(_pendingTitle) || !_enabled || _tray == null)
            {
                _pendingTitle = null;
                _pendingText = null;
                return;
            }

            string title = _pendingTitle;
            string text = _pendingText;
            ToolTipIcon icon = _pendingIcon;
            _pendingTitle = null;
            _pendingText = null;
            try
            {
                _tray.ShowBalloonTip(3500, title, text, icon);
            }
            catch
            {
            }
        }
    }

    internal sealed class TrayController : IDisposable
    {
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _open;
        private readonly ToolStripMenuItem _blackout;
        private readonly ToolStripMenuItem _restore;
        private readonly ToolStripMenuItem _guardStatus;
        private readonly ToolStripMenuItem _repair;
        private readonly ToolStripMenuItem _settings;
        private readonly ToolStripMenuItem _exit;
        private readonly Icon _activeIcon;
        private readonly Icon _degradedIcon;
        private readonly Icon _offIcon;
        private GuardState _guardState = GuardState.Off;

        public event EventHandler OpenRequested;
        public event EventHandler BlackoutRequested;
        public event EventHandler RestoreRequested;
        public event EventHandler RepairRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler ExitRequested;

        public TrayController(AppNotifier notifier)
        {
            _activeIcon = AppIcons.TrayActive;
            if (_activeIcon == null) _activeIcon = TrayIconFactory.Create(AppTheme.Success);
            _degradedIcon = AppIcons.TrayDegraded;
            if (_degradedIcon == null) _degradedIcon = TrayIconFactory.Create(AppTheme.Warning);
            _offIcon = AppIcons.TrayOff;
            if (_offIcon == null) _offIcon = TrayIconFactory.Create(Color.FromArgb(139, 148, 158));

            _open = new ToolStripMenuItem("打开幕帘");
            _open.Click += delegate { Raise(OpenRequested); };
            _blackout = new ToolStripMenuItem("立即熄屏");
            _blackout.Click += delegate { Raise(BlackoutRequested); };
            _restore = new ToolStripMenuItem("停止熄屏并恢复");
            _restore.Enabled = false;
            _restore.Click += delegate { Raise(RestoreRequested); };
            _guardStatus = new ToolStripMenuItem("更新重启保护：未启动");
            _guardStatus.Enabled = false;
            _repair = new ToolStripMenuItem("修复更新设置…");
            _repair.Visible = false;
            _repair.Click += delegate { Raise(RepairRequested); };
            _settings = new ToolStripMenuItem("设置…");
            _settings.Click += delegate { Raise(SettingsRequested); };
            _exit = new ToolStripMenuItem("退出并恢复系统设置…");
            _exit.Click += delegate { Raise(ExitRequested); };

            _menu = new ContextMenuStrip();
            _menu.Items.Add(_open);
            _menu.Items.Add(_blackout);
            _menu.Items.Add(_restore);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_guardStatus);
            _menu.Items.Add(_repair);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_settings);
            _menu.Items.Add(_exit);

            _tray = new NotifyIcon();
            _tray.Icon = _offIcon;
            _tray.Text = "幕帘 ScreenCurtain";
            _tray.Visible = true;
            _tray.ContextMenuStrip = _menu;
            _tray.DoubleClick += delegate { Raise(OpenRequested); };
            notifier.SetTray(_tray);
            Localization.Apply(_menu.Items);
            _tray.Text = Localization.T(_tray.Text);
        }

        public void SetHotkeyText(string text)
        {
            _blackout.Text = Localization.T("立即熄屏") + "    " + text;
            _tray.Text = Localization.T("幕帘 · 熄屏就绪");
        }

        public void SetDisplayCount(int count)
        {
            // Keep tooltip under the Win32 63-character limit.
            _tray.Text = Localization.IsEnglish
                ? "ScreenCurtain · " + GuardShort(_guardState) + " · " + count + " display(s)"
                : "幕帘 · " + GuardShort(_guardState) + " · " + count + " 台显示器";
        }

        public void SetGuardState(GuardState state, string message)
        {
            _guardState = state;
            string text;
            Icon icon;
            if (state == GuardState.Active)
            {
                text = "更新重启保护：已开启";
                icon = _activeIcon;
            }
            else if (state == GuardState.Degraded)
            {
                text = "更新重启保护：降级";
                icon = _degradedIcon;
            }
            else if (state == GuardState.Starting)
            {
                text = "更新重启保护：启动中";
                icon = _degradedIcon;
            }
            else if (state == GuardState.Stopping)
            {
                text = "更新重启保护：停止中";
                icon = _degradedIcon;
            }
            else
            {
                text = "更新重启保护：未启用";
                icon = _offIcon;
            }
            _guardStatus.Text = Localization.T(text) + (string.IsNullOrEmpty(message) ? "" : " · " + Short(message));
            _repair.Visible = state == GuardState.Degraded;
            _tray.Icon = icon;
            _tray.Text = Localization.IsEnglish
                ? "ScreenCurtain · " + GuardShort(state)
                : "幕帘 · " + GuardShort(state);
        }

        public void SetBlackoutState(BlackoutState state)
        {
            _blackout.Enabled = state == BlackoutState.Idle;
            _restore.Enabled = state != BlackoutState.Idle;
            _settings.Enabled = state == BlackoutState.Idle;
            _open.Enabled = state == BlackoutState.Idle;
            _repair.Enabled = state == BlackoutState.Idle && _guardState == GuardState.Degraded;
        }

        public void ShowWindow()
        {
            Raise(OpenRequested);
        }

        public void Dispose()
        {
            try { _tray.Visible = false; } catch { }
            try { _tray.Dispose(); } catch { }
            try { _menu.Dispose(); } catch { }
            try { _activeIcon.Dispose(); } catch { }
            try { _degradedIcon.Dispose(); } catch { }
            try { _offIcon.Dispose(); } catch { }
        }

        private static string GuardShort(GuardState state)
        {
            switch (state)
            {
                case GuardState.Active: return Localization.T("保护已开启");
                case GuardState.Degraded: return Localization.T("保护降级");
                case GuardState.Starting: return Localization.T("保护启动中");
                case GuardState.Stopping: return Localization.T("保护停止中");
                default: return Localization.T("保护未启用");
            }
        }

        private static string Short(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length > 42 ? text.Substring(0, 39) + "..." : text;
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                try { handler(this, EventArgs.Empty); } catch (Exception ex) { Log.Exception("托盘菜单回调失败", ex); }
            }
        }
    }

    internal static class TrayIconFactory
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon Create(Color statusColor)
        {
            try
            {
                using (Bitmap bitmap = new Bitmap(32, 32))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Icon baseIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    if (baseIcon != null)
                    {
                        g.DrawIcon(baseIcon, new Rectangle(0, 0, 32, 32));
                        baseIcon.Dispose();
                    }
                    else
                    {
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(11, 27, 43)))
                        {
                            g.FillEllipse(brush, 1, 1, 30, 30);
                        }
                    }
                    using (SolidBrush dot = new SolidBrush(statusColor))
                    using (Pen outline = new Pen(Color.White, 2))
                    {
                        g.FillEllipse(dot, 21, 21, 10, 10);
                        g.DrawEllipse(outline, 21, 21, 10, 10);
                    }
                    IntPtr handle = bitmap.GetHicon();
                    Icon icon = (Icon)Icon.FromHandle(handle).Clone();
                    DestroyIcon(handle);
                    return icon;
                }
            }
            catch
            {
                return (Icon)SystemIcons.Application.Clone();
            }
        }
    }
}
