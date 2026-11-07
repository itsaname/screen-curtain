using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class SettingsForm : Form
    {
        private readonly Settings _settings;
        private readonly UpdateGuardClient _guard;
        private readonly MainForm _owner;
        private readonly ListBox _nav;
        private readonly Panel _pagePanel;
        private readonly Button _closeButton;
        private bool _loading;
        private int _page;
        private Label _guardStatusLabel;
        private Label _guardMessageLabel;
        private ToggleSwitch _guardToggle;
        private Button _guardAction;
        private Button _guardRepair;
        private Label _hotkeyError;
        private HotkeyCaptureBox _hotkeyBox;

        public SettingsForm(Settings settings, UpdateGuardClient guard, MainForm owner, int initialPage)
        {
            _settings = settings;
            _guard = guard;
            _owner = owner;
            _guard.StatusChanged += OnGuardStatusChanged;

            Text = "设置 - 幕帘";
            Font = AppTheme.BodyFont;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(850, 620);
            BackColor = AppTheme.WindowBackground;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label title = AppTheme.CreateLabel("设置", new Font(AppTheme.BodyFont.FontFamily, 19F, FontStyle.Bold), AppTheme.TextPrimary);
            title.SetBounds(24, 16, 200, 38);
            Controls.Add(title);

            Label subtitle = AppTheme.CreateLabel("设置会自动保存；高风险操作会先确认。", AppTheme.SmallFont, AppTheme.TextSecondary);
            subtitle.SetBounds(26, 54, 500, 20);
            Controls.Add(subtitle);

            _nav = new ListBox();
            _nav.Font = new Font(AppTheme.BodyFont.FontFamily, 10F, FontStyle.Regular);
            _nav.ItemHeight = 38;
            _nav.BorderStyle = BorderStyle.None;
            _nav.BackColor = AppTheme.Surface;
            _nav.SetBounds(20, 90, 180, 440);
            _nav.Items.Add(Localization.T("常规"));
            _nav.Items.Add(Localization.T("熄屏"));
            _nav.Items.Add(Localization.T("更新保护"));
            _nav.Items.Add(Localization.T("诊断与关于"));
            _nav.SelectedIndexChanged += delegate { if (!_loading) ShowPage(_nav.SelectedIndex); };
            Controls.Add(_nav);

            _pagePanel = new Panel();
            _pagePanel.SetBounds(214, 90, 616, 440);
            _pagePanel.BackColor = AppTheme.Surface;
            _pagePanel.BorderStyle = BorderStyle.FixedSingle;
            _pagePanel.AutoScroll = true;
            Controls.Add(_pagePanel);

            _closeButton = UiLayout.PrimaryButton("完成");
            _closeButton.SetBounds(730, 550, 96, 38);
            _closeButton.Click += delegate { Close(); };
            Controls.Add(_closeButton);

            Label hint = AppTheme.CreateLabel("设置自动保存到数据目录；路径见“诊断与关于”。", AppTheme.SmallFont, AppTheme.TextSecondary);
            hint.SetBounds(22, 560, 660, 20);
            Controls.Add(hint);

            _page = Math.Max(0, Math.Min(3, initialPage));
            _loading = true;
            _nav.SelectedIndex = _page;
            _loading = false;
            ShowPage(_page);
            Load += delegate { UiDpi.Apply(this); };
            FormClosed += delegate { try { _guard.StatusChanged -= OnGuardStatusChanged; } catch { } };
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index > 3) return;
            _page = index;
            _loading = true;
            _pagePanel.SuspendLayout();
            _pagePanel.Controls.Clear();
            if (index == 0) BuildGeneralPage();
            else if (index == 1) BuildScreenPage();
            else if (index == 2) BuildGuardPage();
            else BuildDiagnosticsPage();
            _pagePanel.ResumeLayout();
            _loading = false;
            Localization.Apply(this);
        }

        private void BuildGeneralPage()
        {
            int y = 22;
            AddSection("界面语言 / Language", ref y);
            AddLabel("界面语言", "切换后重启程序生效。", ref y);
            ComboBox language = new ComboBox();
            language.DropDownStyle = ComboBoxStyle.DropDownList;
            language.Items.Add("自动 / Auto");
            language.Items.Add("中文");
            language.Items.Add("English");
            language.SelectedIndex = _settings.Language == "en" ? 2 : (_settings.Language == "zh" ? 1 : 0);
            language.SetBounds(22, y, 220, 28);
            language.SelectedIndexChanged += delegate
            {
                if (_loading) return;
                string code = language.SelectedIndex == 2 ? "en" : (language.SelectedIndex == 1 ? "zh" : "auto");
                _settings.Language = code;
                _settings.Save();
                MessageBox.Show(this,
                    Localization.T("界面语言将在重启程序后生效。", "The UI language will change after restarting the app."),
                    Localization.T("界面语言", "Language"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _pagePanel.Controls.Add(language);
            y += 42;

            AddSection("窗口与启动", ref y);
            AddToggle("启动后最小化到任务栏", "启动时不抢占焦点；更新保护仍会在后台尝试启动。", ref y, _settings.StartMinimized, delegate(bool value)
            {
                _settings.StartMinimized = value;
                _settings.Save();
            });

            AddLabel("关闭主窗口时", "保护开启时无论此选项如何都会最小化到任务栏，以保证关机否决的可见窗口。", ref y);
            ComboBox closeAction = new ComboBox();
            closeAction.DropDownStyle = ComboBoxStyle.DropDownList;
            closeAction.Items.Add(Localization.T("最小化到任务栏"));
            closeAction.Items.Add(Localization.T("退出幕帘"));
            closeAction.SelectedIndex = _settings.CloseAction == CloseAction.Exit ? 1 : 0;
            closeAction.SetBounds(22, y, 220, 28);
            closeAction.SelectedIndexChanged += delegate
            {
                if (_loading) return;
                _settings.CloseAction = closeAction.SelectedIndex == 1 ? CloseAction.Exit : CloseAction.Minimize;
                _settings.Save();
            };
            _pagePanel.Controls.Add(closeAction);
            y += 42;

            AddToggle("退出前确认", "在退出、关闭保护或恢复失败时显示确认提示。", ref y, _settings.ConfirmExit, delegate(bool value)
            {
                _settings.ConfirmExit = value;
                _settings.Save();
            });
            AddToggle("显示托盘通知", "最小化、保护降级等提示。黑屏期间始终不会显示。", ref y, _settings.ShowTrayNotifications, delegate(bool value)
            {
                _settings.ShowTrayNotifications = value;
                _settings.Save();
            });

            AddInfo("开机自启、深色主题和英文界面属于后续版本，本版本不会显示不可用的开关。", ref y);
        }

        private void BuildScreenPage()
        {
            int y = 22;
            AddSection("快捷键", ref y);
            AddLabel("熄屏快捷键", "点击后按下组合键；至少包含 Ctrl/Alt/Shift/Win 之一。", ref y);
            _hotkeyBox = new HotkeyCaptureBox();
            _hotkeyBox.SetBounds(22, y, 260, 36);
            _hotkeyBox.Spec = _settings.GetHotkey();
            _hotkeyBox.HotkeyChanged += delegate
            {
                if (_loading) return;
                string error;
                if (!_owner.TryChangeHotkey(_hotkeyBox.Spec, out error))
                {
                    _hotkeyError.Text = error;
                    _hotkeyError.Visible = true;
                    _hotkeyBox.Spec = _settings.GetHotkey();
                }
                else
                {
                    _hotkeyError.Visible = false;
                }
            };
            _pagePanel.Controls.Add(_hotkeyBox);
            Button resetHotkey = new Button();
            AppTheme.StyleLink(resetHotkey);
            resetHotkey.Text = "恢复默认";
            resetHotkey.SetBounds(292, y + 2, 90, 30);
            resetHotkey.Click += delegate
            {
                string error;
                if (!_owner.TryChangeHotkey(HotkeySpec.Default, out error))
                {
                    _hotkeyError.Text = error;
                    _hotkeyError.Visible = true;
                    return;
                }
                _hotkeyBox.Spec = HotkeySpec.Default;
                _hotkeyError.Visible = false;
            };
            _pagePanel.Controls.Add(resetHotkey);
            y += 44;

            _hotkeyError = AppTheme.CreateLabel(string.Empty, AppTheme.SmallFont, AppTheme.Danger);
            _hotkeyError.SetBounds(22, y, 550, 24);
            _hotkeyError.Visible = false;
            _pagePanel.Controls.Add(_hotkeyError);
            y += 30;

            AddSection("熄屏行为", ref y);
            AddToggle("尝试物理关闭显示器", "发送 DPMS 关闭请求。部分显示器/虚拟机不支持，覆盖层仍会保持全黑；笔记本内置屏默认不发送。", ref y, _settings.TryPhysicalOff, delegate(bool value)
            {
                _settings.TryPhysicalOff = value;
                _settings.Save();
            });
            AddToggle("在笔记本上尝试物理关屏", "默认关闭。检测到电池时只使用全屏黑层；开启后可能引起内置屏背光反复亮灭，或唤醒后停留在锁屏。", ref y, _settings.LaptopPhysicalOff, delegate(bool value)
            {
                _settings.LaptopPhysicalOff = value;
                _settings.Save();
            });
            AddToggle("屏幕仅关闭背光（内置 WMI / 外接 DDC）", "默认开启。外接屏通过 DDC/CI VCP 0x10 将亮度设为 0；内置屏通过 WMI WmiSetBrightness 尝试设为 0。显示器保持供电，USB Hub 和键鼠继续工作；不支持时保留全屏黑层。", ref y, _settings.DimExternalBacklight, delegate(bool value)
            {
                _settings.DimExternalBacklight = value;
                _settings.Save();
            });
            AddInfo(SystemPower.HasBattery
                ? "当前检测到电池：默认不会发送物理关屏，避免笔记本内置屏背光反复和唤醒锁屏。"
                : "当前未检测到电池：物理关屏按“尝试物理关闭显示器”设置执行。", ref y);
            AddInfo("如果系统启用了“唤醒时要求登录”或强制空闲锁定策略，黑屏结束后可能仍需登录；这是 Windows 安全策略，本程序不会尝试绕过。", ref y);
            AddToggle("保持系统唤醒", "熄屏期间阻止系统自动睡眠；不会阻止系统关机。", ref y, _settings.PreventSleep, delegate(bool value)
            {
                _settings.PreventSleep = value;
                _settings.Save();
            });
            AddToggle("允许鼠标滚轮恢复", "默认只有点击鼠标或按键恢复，避免轻微滚动唤醒。", ref y, _settings.AllowWheelWake, delegate(bool value)
            {
                _settings.AllowWheelWake = value;
                _settings.Save();
            });
            AddToggle("恢复时还原前台窗口", "恢复前记忆当前窗口，并尝试切回。", ref y, _settings.RestoreForeground, delegate(bool value)
            {
                _settings.RestoreForeground = value;
                _settings.Save();
            });
            AddToggle("熄屏时静音系统音频", "静音所有活动播放设备；恢复显示时还原之前的状态。默认开启。", ref y, _settings.MuteSystemAudio, delegate(bool value)
            {
                _settings.MuteSystemAudio = value;
                _settings.Save();
            });

            AddSection("显示器与预览", ref y);
            int count;
            try { count = DisplayManager.Enumerate().Count; } catch { count = 0; }
            AddInfo(Localization.IsEnglish
                ? "Detected " + count + " display(s). The black layer covers every display, including the taskbar and notification area."
                : "当前检测到 " + count + " 台显示器。黑屏覆盖所有显示器，并覆盖任务栏/系统托盘。", ref y);

            try
            {
                System.Collections.Generic.List<DisplayInfo> displays = DisplayManager.Enumerate();
                StringBuilder monitorText = new StringBuilder();
                for (int i = 0; i < displays.Count; i++)
                {
                    DisplayInfo d = displays[i];
                    string kind = d.IsLikelyLaptopInternal
                        ? Localization.T("内置屏（WMI 背光 0）")
                        : d.IsLikelyLaptopExternal
                            ? Localization.T("外接屏（DDC 背光 0）")
                            : Localization.T("尺寸未知（不自动调背光）");
                    string source = string.IsNullOrEmpty(d.DetectionSource)
                        ? Localization.T("尺寸判断")
                        : d.DetectionSource + (string.IsNullOrEmpty(d.ConnectionTechnology) ? "" : " / " + d.ConnectionTechnology);
                    monitorText.AppendLine(string.Format(
                        "{0}  {1:0.0}″  {2}  [{3}]",
                        d.DeviceName,
                        d.DiagonalInches,
                        kind,
                        source));
                }
                AddInfo(monitorText.ToString().TrimEnd(), ref y);
            }
            catch
            {
            }

            Button preview = UiLayout.SecondaryButton("预览 3 秒");
            preview.SetBounds(22, y, 120, 34);
            preview.Click += delegate { _owner.PreviewBlackout(3); };
            _pagePanel.Controls.Add(preview);
            y += 44;
        }

        private void BuildGuardPage()
        {
            int y = 22;
            AddSection("更新重启保护", ref y);
            AddInfo("启用后需要管理员权限。保护仅在幕帘运行期间临时暂停自动更新，退出或关闭保护时恢复原设置。", ref y);

            _guardStatusLabel = AppTheme.CreateLabel("状态：读取中", AppTheme.SectionFont, AppTheme.TextPrimary);
            _guardStatusLabel.SetBounds(22, y, 550, 28);
            _pagePanel.Controls.Add(_guardStatusLabel);
            y += 34;

            _guardMessageLabel = AppTheme.CreateLabel(string.Empty, AppTheme.SmallFont, AppTheme.TextSecondary);
            _guardMessageLabel.SetBounds(22, y, 550, 44);
            _pagePanel.Controls.Add(_guardMessageLabel);
            y += 50;

            _guardToggle = new ToggleSwitch();
            _guardToggle.SetBounds(22, y, 42, 24);
            _guardToggle.CheckedChanged += delegate
            {
                if (_loading) return;
                if (_guardToggle.Checked)
                {
                    _owner.SetGuardEnabled(true);
                }
                else
                {
                    bool confirmed = ConfirmDialog.Show(
                        this,
                        "关闭更新重启保护？",
                        "幕帘会恢复 Windows Update 原始设置。若更新设置恢复失败，会保留降级状态并允许重试。",
                        "关闭保护",
                        false);
                    if (!confirmed)
                    {
                        _loading = true;
                        _guardToggle.Checked = true;
                        _loading = false;
                        return;
                    }
                    _owner.SetGuardEnabled(false);
                }
                UpdateGuardControls();
            };
            _pagePanel.Controls.Add(_guardToggle);
            Label toggleLabel = AppTheme.CreateLabel("在本程序运行期间启用保护", AppTheme.BodyFont, AppTheme.TextPrimary);
            toggleLabel.SetBounds(74, y - 2, 320, 28);
            _pagePanel.Controls.Add(toggleLabel);
            y += 40;

            _guardAction = UiLayout.SecondaryButton("启用保护");
            _guardAction.SetBounds(22, y, 120, 34);
            _guardAction.Click += delegate { _owner.SetGuardEnabled(true); UpdateGuardControls(); };
            _pagePanel.Controls.Add(_guardAction);

            _guardRepair = UiLayout.SecondaryButton("修复更新设置");
            _guardRepair.SetBounds(154, y, 130, 34);
            _guardRepair.Click += delegate { _owner.SetGuardEnabled(true); UpdateGuardControls(); };
            _pagePanel.Controls.Add(_guardRepair);
            y += 48;

            AddInfo("“更新保护降级”通常表示管理员权限被取消，或退出时未能恢复原更新策略。修复会再次请求 UAC，并先恢复遗留快照再重新建立保护。", ref y);
            AddInfo("幕帘无法阻止用户在系统界面选择“仍要重启”，也无法阻止组策略/强制断电。", ref y);

            UpdateGuardControls();
        }

        private void BuildDiagnosticsPage()
        {
            int y = 22;
            AddSection("诊断信息", ref y);
            int count;
            try { count = DisplayManager.Enumerate().Count; } catch { count = 0; }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine((Localization.IsEnglish ? "Version: " : "版本：") + ReleaseInfo.FullVersion);
            sb.AppendLine((Localization.IsEnglish ? "System: " : "系统：") + Environment.OSVersion.VersionString + "（" + (Environment.Is64BitProcess ? Localization.T("64 位进程") : Localization.T("32 位进程")) + "）");
            sb.AppendLine(".NET：" + Environment.Version);
            sb.AppendLine((Localization.IsEnglish ? "Displays: " : "显示器：") + count + (Localization.IsEnglish ? " display(s)" : " 台"));
            sb.AppendLine((Localization.IsEnglish ? "Data directory: " : "数据目录：") + AppPaths.DataDirectory);
            sb.AppendLine((Localization.IsEnglish ? "Config file: " : "配置文件：") + AppPaths.SettingsFile);
            sb.AppendLine((Localization.IsEnglish ? "Log directory: " : "日志目录：") + AppPaths.LogDirectory);
            sb.AppendLine((Localization.IsEnglish ? "Portable: " : "便携模式：") + (AppPaths.IsPortable ? Localization.T("是") : Localization.T("否")) + (AppPaths.PortableFallback ? Localization.T("（已回退到 LocalAppData）") : ""));
            sb.AppendLine((Localization.IsEnglish ? "Update protection: " : "更新保护：") + _guard.State + " " + _guard.Message);
            AddInfo(sb.ToString().TrimEnd(), ref y);

            Button copy = UiLayout.SecondaryButton("复制诊断信息");
            copy.SetBounds(22, y, 140, 34);
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(sb.ToString());
                    _owner.RefreshFromSettings();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "复制失败：" + ex.Message, "幕帘", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            _pagePanel.Controls.Add(copy);
            Button logs = UiLayout.SecondaryButton("打开日志目录");
            logs.SetBounds(174, y, 140, 34);
            logs.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo { FileName = Log.DirectoryPath, UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, Localization.T("无法打开日志目录：") + ex.Message, Localization.T("幕帘"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            _pagePanel.Controls.Add(logs);
            y += 46;

            AddSection("重置", ref y);
            AddInfo("恢复默认设置会关闭当前更新保护并恢复策略，然后重置熄屏、窗口、快捷键和通知选项。不会删除日志。", ref y);
            Button reset = UiLayout.SecondaryButton("恢复默认设置");
            reset.ForeColor = AppTheme.Danger;
            reset.SetBounds(22, y, 150, 34);
            reset.Click += delegate { ResetSettings(); };
            _pagePanel.Controls.Add(reset);
            y += 46;

            AddSection("关于", ref y);
            AddInfo("幕帘 ScreenCurtain " + ReleaseInfo.Version + "。一个不锁屏、不驻留驱动、不改系统服务的屏幕黑屏与更新重启保护工具。", ref y);
        }

        private void ResetSettings()
        {
            if (!ConfirmDialog.Show(this, "恢复默认设置？", "当前更新保护会先关闭并恢复系统策略；如果恢复失败，请先处理错误再重置。", "恢复默认", true))
            {
                return;
            }

            try
            {
                if (_owner.GuardStateValue == GuardState.Active || _owner.GuardStateValue == GuardState.Degraded || _owner.GuardStateValue == GuardState.Starting)
                {
                    _owner.SetGuardEnabled(false);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("重置设置时关闭更新保护失败", ex);
            }

            _settings.GuardEnabled = false;
            _settings.AllowWheelWake = false;
            _settings.PreventSleep = true;
            _settings.StartMinimized = false;
            _settings.TryPhysicalOff = true;
            _settings.LaptopPhysicalOff = false;
            _settings.DimExternalBacklight = true;
            _settings.RestoreForeground = true;
            _settings.CloseAction = CloseAction.Minimize;
            _settings.ConfirmExit = true;
            _settings.ShowTrayNotifications = true;
            _settings.MuteSystemAudio = true;
            _settings.Hotkey = Settings.DefaultHotkey;
            _settings.FirstRun = false;
            _settings.Save();

            string error;
            _owner.TryChangeHotkey(HotkeySpec.Default, out error);
            _owner.RefreshFromSettings();
            Close();
        }

        private void UpdateGuardControls()
        {
            if (_guardStatusLabel == null || _guardStatusLabel.IsDisposed) return;
            GuardState state = _guard.State;
            string status;
            Color color;
            if (state == GuardState.Active) { status = "已开启"; color = AppTheme.Success; }
            else if (state == GuardState.Starting) { status = "正在启动"; color = AppTheme.Warning; }
            else if (state == GuardState.Degraded) { status = "降级 · 需要修复"; color = AppTheme.Warning; }
            else if (state == GuardState.Stopping) { status = "正在恢复"; color = AppTheme.Warning; }
            else { status = "未启用"; color = AppTheme.TextSecondary; }

            _guardStatusLabel.Text = Localization.T("状态：") + Localization.T(status);
            _guardStatusLabel.ForeColor = color;
            _guardMessageLabel.Text = _guard.Message ?? string.Empty;
            _guardAction.Visible = state == GuardState.Degraded || state == GuardState.Off;
            _guardAction.Text = state == GuardState.Degraded ? "重试保护" : "启用保护";
            _guardRepair.Visible = state == GuardState.Degraded;

            _loading = true;
            _guardToggle.Checked = state == GuardState.Active || state == GuardState.Starting || state == GuardState.Degraded;
            _loading = false;
        }

        private void OnGuardStatusChanged(object sender, GuardStatusEventArgs e)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) { BeginInvoke(new Action(UpdateGuardControls)); return; }
                UpdateGuardControls();
            }
            catch { }
        }

        private void AddSection(string text, ref int y)
        {
            Label label = AppTheme.CreateLabel(text, AppTheme.SectionFont, AppTheme.TextPrimary);
            label.SetBounds(22, y, 550, 26);
            _pagePanel.Controls.Add(label);
            y += 32;
        }

        private void AddLabel(string text, string description, ref int y)
        {
            Label label = AppTheme.CreateLabel(text, AppTheme.BodyFont, AppTheme.TextPrimary);
            label.SetBounds(22, y, 550, 22);
            _pagePanel.Controls.Add(label);
            y += 22;
            if (!string.IsNullOrEmpty(description))
            {
                Label desc = AppTheme.CreateLabel(description, AppTheme.SmallFont, AppTheme.TextSecondary);
                desc.SetBounds(22, y, 550, 48);
                _pagePanel.Controls.Add(desc);
                y += 48;
            }
            y += 8;
        }

        private void AddInfo(string text, ref int y)
        {
            Label label = AppTheme.CreateLabel(text, AppTheme.SmallFont, AppTheme.TextSecondary);
            Size measured = TextRenderer.MeasureText(text, label.Font, new Size(550, int.MaxValue), TextFormatFlags.WordBreak);
            label.SetBounds(22, y, 550, Math.Max(34, measured.Height + 12));
            _pagePanel.Controls.Add(label);
            y += label.Height + 10;
        }

        private void AddToggle(string title, string description, ref int y, bool value, Action<bool> changed)
        {
            Label label = AppTheme.CreateLabel(title, AppTheme.BodyFont, AppTheme.TextPrimary);
            label.SetBounds(22, y, 470, 22);
            _pagePanel.Controls.Add(label);
            Label desc = AppTheme.CreateLabel(description, AppTheme.SmallFont, AppTheme.TextSecondary);
            desc.SetBounds(22, y + 22, 480, 46);
            _pagePanel.Controls.Add(desc);
            ToggleSwitch toggle = new ToggleSwitch();
            toggle.SetBounds(530, y + 2, 42, 24);
            toggle.Checked = value;
            toggle.CheckedChanged += delegate
            {
                if (_loading) return;
                changed(toggle.Checked);
            };
            _pagePanel.Controls.Add(toggle);
            y += 72;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _guard.StatusChanged -= OnGuardStatusChanged; } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
