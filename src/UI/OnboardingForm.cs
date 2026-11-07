using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class OnboardingForm : Form
    {
        private readonly Settings _settings;
        private readonly UpdateGuardClient _guard;
        private readonly Label _stepLabel;
        private readonly Label _titleLabel;
        private readonly CardPanel _card;
        private readonly Button _backButton;
        private readonly Button _nextButton;
        private int _step = 1;
        private bool _allowClose;
        private bool _completed;
        private bool _startingGuard;
        private CheckBox _ack;
        private RadioButton _enableGuard;
        private RadioButton _screenOnly;
        private Label _guardStatus;
        private Label _error;
        private Button _skipGuard;
        private HotkeyCaptureBox _hotkey;
        private ToggleSwitch _startMinimized;

        public event EventHandler Completed;
        public event EventHandler Canceled;

        public OnboardingForm(Settings settings, UpdateGuardClient guard)
        {
            _settings = settings;
            _guard = guard;
            _guard.StatusChanged += OnGuardStatusChanged;

            Text = "幕帘 - 首次使用";
            Font = AppTheme.BodyFont;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(680, 560);
            BackColor = AppTheme.WindowBackground;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label brand = AppTheme.CreateLabel("幕帘", AppTheme.TitleFont, AppTheme.TextPrimary);
            brand.SetBounds(26, 18, 180, 40);
            Controls.Add(brand);

            Label brandSub = AppTheme.CreateLabel("让所有显示器安静下来", AppTheme.SmallFont, AppTheme.TextSecondary);
            brandSub.SetBounds(28, 56, 260, 20);
            Controls.Add(brandSub);

            _stepLabel = AppTheme.CreateLabel("第 1 步，共 3 步", AppTheme.SmallFont, AppTheme.TextSecondary);
            _stepLabel.TextAlign = ContentAlignment.MiddleRight;
            _stepLabel.SetBounds(460, 28, 194, 22);
            Controls.Add(_stepLabel);

            _card = new CardPanel();
            _card.SetBounds(24, 88, 632, 356);
            _card.Padding = new Padding(28);
            Controls.Add(_card);

            _titleLabel = AppTheme.CreateLabel(string.Empty, AppTheme.HeroFont, AppTheme.TextPrimary);
            _titleLabel.SetBounds(28, 22, 576, 40);
            _card.Controls.Add(_titleLabel);

            _backButton = UiLayout.SecondaryButton("上一步");
            _backButton.SetBounds(24, 468, 92, 36);
            _backButton.Click += delegate { GoBack(); };
            Controls.Add(_backButton);

            _nextButton = UiLayout.PrimaryButton("继续");
            _nextButton.SetBounds(476, 468, 180, 40);
            _nextButton.Click += delegate { GoNext(); };
            Controls.Add(_nextButton);

            Label footer = AppTheme.CreateLabel("设置可以随时在“设置”中修改。", AppTheme.SmallFont, AppTheme.TextSecondary);
            footer.SetBounds(132, 476, 330, 22);
            Controls.Add(footer);

            FormClosing += OnFormClosing;
            Load += delegate { UiDpi.Apply(this); };
            FormClosed += delegate
            {
                if (!_completed && Canceled != null)
                {
                    Canceled(this, EventArgs.Empty);
                }
            };

            ShowStep(1);
            for (int i = 0; i < Environment.GetCommandLineArgs().Length; i++)
            {
                string arg = Environment.GetCommandLineArgs()[i];
                if (arg.StartsWith("--onboarding-step=", StringComparison.OrdinalIgnoreCase))
                {
                    int step;
                    if (int.TryParse(arg.Substring("--onboarding-step=".Length), out step) && step >= 1 && step <= 3)
                    {
                        ShowStep(step);
                    }
                }
            }
        }

        public void CloseForCompletion()
        {
            _allowClose = true;
            Close();
        }

        public void CloseForExit()
        {
            _allowClose = true;
            Close();
        }

        private void ShowStep(int step)
        {
            _step = step;
            _stepLabel.Text = Localization.IsEnglish
                ? "Step " + step + " of 3"
                : "第 " + step + " 步，共 3 步";
            _card.Controls.Clear();
            _card.Controls.Add(_titleLabel);
            _titleLabel.Text = Localization.IsEnglish
                ? (step == 1 ? "First, understand the boundaries" : step == 2 ? "Enable update restart protection?" : "Set your shortcut")
                : (step == 1 ? "先了解幕帘的边界" : step == 2 ? "要不要启用更新重启保护？" : "设置你的快捷方式");
            _backButton.Visible = step > 1;
            _nextButton.Enabled = true;
            _nextButton.Text = Localization.IsEnglish
                ? (step == 1 ? "Continue" : step == 2 && _enableGuard != null && _enableGuard.Checked ? "Enable and continue" : step == 3 ? "Done" : "Blackout only")
                : (step == 1 ? "继续" : step == 2 && _enableGuard != null && _enableGuard.Checked ? "启用并继续" : step == 3 ? "完成" : "仅熄屏继续");

            if (step == 1) BuildStepOne();
            else if (step == 2) BuildStepTwo();
            else BuildStepThree();
            Localization.Apply(this);
        }

        private void BuildStepOne()
        {
            _ack = new CheckBox();
            _ack.Text = "我了解：黑屏不是锁屏，熄屏时 Windows 会话仍然保持解锁。";
            _ack.Font = AppTheme.BodyFont;
            _ack.ForeColor = AppTheme.TextPrimary;
            _ack.BackColor = Color.Transparent;
            _ack.SetBounds(28, 286, 570, 30);
            _ack.CheckedChanged += delegate { _nextButton.Enabled = _ack.Checked; };
            _card.Controls.Add(_ack);
            _nextButton.Enabled = false;

            AddParagraph("点击“立即熄屏”后，幕帘会覆盖所有显示器，并尽力让显示器进入物理关闭。鼠标移动不会退出，点击鼠标或按键盘会恢复。", 28, 76, 576, 62);
            AddParagraph("熄屏期间系统保持唤醒、当前会话保持解锁，因此这不是安全锁屏。如果需要离开座位，请先按 Win+L 锁屏。", 28, 146, 576, 58);
            AddParagraph("下一步可以选择是否启用更新重启保护。启用需要管理员权限；它只在幕帘运行期间临时暂停自动更新，退出时会恢复原设置。", 28, 212, 576, 64);
        }

        private void BuildStepTwo()
        {
            _enableGuard = new RadioButton();
            _enableGuard.Text = "启用更新重启保护（推荐）";
            _enableGuard.Font = new Font(AppTheme.BodyFont, FontStyle.Bold);
            _enableGuard.ForeColor = AppTheme.TextPrimary;
            _enableGuard.BackColor = Color.Transparent;
            _enableGuard.SetBounds(28, 76, 430, 26);
            _card.Controls.Add(_enableGuard);

            AddParagraph("需要一次管理员权限（UAC）。保护期间临时暂停 Windows 自动更新，并阻止运行期间的更新重启；退出幕帘时恢复原策略。", 48, 104, 548, 58);

            _screenOnly = new RadioButton();
            _screenOnly.Text = "仅使用熄屏，不修改系统更新设置";
            _screenOnly.Font = AppTheme.BodyFont;
            _screenOnly.ForeColor = AppTheme.TextPrimary;
            _screenOnly.BackColor = Color.Transparent;
            _screenOnly.SetBounds(28, 170, 430, 26);
            _card.Controls.Add(_screenOnly);

            AddParagraph("不需要管理员权限。熄屏功能仍可用，但系统自动更新可能在你离开时触发重启。", 48, 198, 548, 58);

            _guardStatus = AppTheme.CreateLabel("尚未启用更新保护", AppTheme.SmallFont, AppTheme.TextSecondary);
            _guardStatus.SetBounds(28, 264, 560, 24);
            _card.Controls.Add(_guardStatus);

            _error = AppTheme.CreateLabel(string.Empty, AppTheme.SmallFont, AppTheme.Danger);
            _error.SetBounds(28, 288, 560, 24);
            _card.Controls.Add(_error);

            _skipGuard = new Button();
            AppTheme.StyleLink(_skipGuard);
            _skipGuard.Text = "仍然仅使用熄屏继续";
            _skipGuard.SetBounds(28, 314, 230, 28);
            _skipGuard.Visible = false;
            _skipGuard.Click += delegate
            {
                _settings.GuardEnabled = false;
                _settings.Save();
                ShutdownGuard.Active = false;
                try { ShutdownGuard.Clear(Handle); } catch { }
                ShowStep(3);
            };
            _card.Controls.Add(_skipGuard);

            _enableGuard.Checked = _settings.GuardEnabled;
            _screenOnly.Checked = !_settings.GuardEnabled;
            _enableGuard.CheckedChanged += delegate { UpdateStepTwoButtons(); };
            _screenOnly.CheckedChanged += delegate { UpdateStepTwoButtons(); };
            if (_guard.State == GuardState.Active)
            {
                _guardStatus.Text = Localization.T("更新保护已经开启");
                _enableGuard.Checked = true;
                _nextButton.Text = Localization.T("继续");
            }
            UpdateStepTwoButtons();
        }

        private void UpdateStepTwoButtons()
        {
            if (_nextButton == null) return;
            _nextButton.Text = Localization.IsEnglish
                ? (_enableGuard != null && _enableGuard.Checked
                    ? (_guard.State == GuardState.Degraded ? "Retry" : "Enable and continue")
                    : "Blackout only")
                : (_enableGuard != null && _enableGuard.Checked
                    ? (_guard.State == GuardState.Degraded ? "重试" : "启用并继续")
                    : "仅熄屏继续");
            _nextButton.Enabled = !_startingGuard;
        }

        private void BuildStepThree()
        {
            AddParagraph("选择一个全局快捷键。在任何应用中使用它，都可以立即进入黑屏。", 28, 74, 576, 40);

            Label label = AppTheme.CreateLabel("熄屏快捷键", AppTheme.SectionFont, AppTheme.TextPrimary);
            label.SetBounds(28, 126, 220, 26);
            _card.Controls.Add(label);

            _hotkey = new HotkeyCaptureBox();
            _hotkey.SetBounds(28, 156, 250, 38);
            _hotkey.Spec = _settings.GetHotkey();
            _card.Controls.Add(_hotkey);
            AddParagraph("点击输入框后再按下组合键；至少包含 Ctrl / Alt / Shift / Win 之一。按 Esc 取消录制。", 28, 200, 570, 42);

            Label opt = AppTheme.CreateLabel("启动后最小化", AppTheme.SectionFont, AppTheme.TextPrimary);
            opt.SetBounds(28, 244, 240, 26);
            _card.Controls.Add(opt);
            _startMinimized = new ToggleSwitch();
            _startMinimized.SetBounds(28, 274, 42, 24);
            _startMinimized.Checked = _settings.StartMinimized;
            _card.Controls.Add(_startMinimized);
            AddParagraph("启用后启动时不抢占焦点；如果更新保护已开启，UAC 仍可能出现在启动时。", 84, 272, 500, 54);

            Label summary = AppTheme.CreateLabel(
                _settings.GuardEnabled
                    ? "将启用更新重启保护。退出幕帘时会恢复系统更新设置。"
                    : "不会修改 Windows 更新设置；随时可以在设置中启用保护。",
                AppTheme.SmallFont,
                _settings.GuardEnabled ? AppTheme.Success : AppTheme.TextSecondary);
            summary.SetBounds(28, 332, 560, 22);
            _card.Controls.Add(summary);
            ActiveControl = _nextButton;
        }

        private void AddParagraph(string text, int x, int y, int width, int height)
        {
            Label label = AppTheme.CreateLabel(text, AppTheme.BodyFont, AppTheme.TextSecondary);
            label.SetBounds(x, y, width, height);
            _card.Controls.Add(label);
        }

        private void GoBack()
        {
            if (_step == 2)
            {
                ShowStep(1);
            }
            else if (_step == 3)
            {
                ShowStep(2);
            }
        }

        private void GoNext()
        {
            if (_step == 1)
            {
                if (_ack == null || !_ack.Checked)
                {
                    return;
                }
                ShowStep(2);
                return;
            }

            if (_step == 2)
            {
                if (_screenOnly != null && _screenOnly.Checked)
                {
                    _settings.GuardEnabled = false;
                    _settings.Save();
                    if (_guard.State == GuardState.Active || _guard.State == GuardState.Degraded || _guard.State == GuardState.Starting)
                    {
                        _guard.Stop();
                    }
                    ShutdownGuard.Active = false;
                    try { ShutdownGuard.Clear(Handle); } catch { }
                    ShowStep(3);
                    return;
                }

                if (_guard.State == GuardState.Active)
                {
                    _settings.GuardEnabled = true;
                    _settings.Save();
                    ShowStep(3);
                    return;
                }

                _settings.GuardEnabled = true;
                _settings.Save();
                ShutdownGuard.Active = true;
                try
                {
                    Native.ShutdownBlockReasonCreate(Handle, ShutdownGuard.Reason);
                }
                catch { }
                _startingGuard = true;
                _nextButton.Enabled = false;
                _nextButton.Text = Localization.T("正在请求权限…");
                _error.Text = string.Empty;
                _guardStatus.Text = Localization.T("正在请求管理员权限，请稍候…");
                if (!_guard.Start())
                {
                    _startingGuard = false;
                    _error.Text = Localization.T("无法启动更新保护。可以重试，或仅使用熄屏继续。");
                    _skipGuard.Visible = true;
                    UpdateStepTwoButtons();
                }
                return;
            }

            if (_step == 3)
            {
                if (_hotkey != null && _hotkey.Spec != null)
                {
                    _settings.Hotkey = _hotkey.Spec.Text;
                }
                if (_startMinimized != null)
                {
                    _settings.StartMinimized = _startMinimized.Checked;
                }
                _settings.FirstRun = false;
                _settings.Save();
                _completed = true;
                if (Completed != null) Completed(this, EventArgs.Empty);
                return;
            }
        }

        private void OnGuardStatusChanged(object sender, GuardStatusEventArgs e)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new EventHandler<GuardStatusEventArgs>(OnGuardStatusChanged), sender, e);
                    return;
                }
            }
            catch
            {
                return;
            }

            if (_guardStatus != null)
            {
                _guardStatus.Text = e.State == GuardState.Active
                    ? Localization.T("更新保护已开启。点击继续完成向导。")
                    : e.State == GuardState.Degraded
                        ? Localization.T("保护未开启：") + Localization.T(e.Message)
                        : Localization.T(e.Message);
            }

            if (!_startingGuard) return;
            if (e.State == GuardState.Active)
            {
                _startingGuard = false;
                ShutdownGuard.Active = true;
                _settings.GuardEnabled = true;
                _settings.Save();
                ShowStep(3);
            }
            else if (e.State == GuardState.Degraded || e.State == GuardState.Off)
            {
                _startingGuard = false;
                _error.Text = e.State == GuardState.Degraded
                    ? Localization.T("管理员权限请求已取消，或其他程序阻止了更新保护。")
                    : Localization.T("更新保护未能启动。");
                _skipGuard.Visible = true;
                _nextButton.Enabled = true;
                _nextButton.Text = Localization.T("重试");
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_QUERYENDSESSION)
            {
                if (ShutdownGuard.HandleQueryEndSession(Handle, unchecked((uint)m.LParam.ToInt64())))
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose || _completed)
            {
                return;
            }

            bool confirmed = ConfirmDialog.Show(
                this,
                "退出首次设置",
                "如果现在退出，幕帘不会保存首次设置，下次启动仍会显示这个向导。已启用的更新保护会被关闭并恢复系统设置。",
                "退出",
                true);
            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }

            _allowClose = true;
            _settings.FirstRun = true;
            _settings.Save();
            ShutdownGuard.Active = false;
            try { ShutdownGuard.Clear(Handle); } catch { }
            // AppHost owns the guard and will stop it after the Canceled event.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _guard.StatusChanged -= OnGuardStatusChanged; } catch { }
                try { ShutdownGuard.Clear(Handle); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
