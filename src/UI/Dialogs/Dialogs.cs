using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal enum RestoreFailureAction
    {
        Retry,
        ExitWithoutRestore,
        Cancel
    }

    internal static class ConfirmDialog
    {
        public static bool Show(IWin32Window owner, string title, string message, string confirmText, bool danger)
        {
            title = Localization.T(title);
            message = Localization.T(message);
            confirmText = Localization.T(confirmText);
            using (Form form = new Form())
            {
                form.Text = title;
                form.Font = AppTheme.BodyFont;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.AutoScaleMode = AutoScaleMode.None;
                form.ClientSize = new Size(460, 220);
                form.BackColor = AppTheme.WindowBackground;
                form.Load += delegate { UiDpi.Apply(form); };

                Label heading = AppTheme.CreateLabel(title, AppTheme.SectionFont, AppTheme.TextPrimary);
                heading.SetBounds(22, 18, 416, 28);
                form.Controls.Add(heading);

                Label body = AppTheme.CreateLabel(message, AppTheme.BodyFont, AppTheme.TextSecondary);
                body.SetBounds(22, 52, 416, 94);
                form.Controls.Add(body);

                Button cancel = UiLayout.SecondaryButton(Localization.T("取消"));
                Button ok = UiLayout.PrimaryButton(confirmText);
                if (danger) ok.BackColor = AppTheme.Danger;

                int okWidth = Math.Max(90, TextRenderer.MeasureText(confirmText, ok.Font).Width + 28);
                int cancelWidth = Math.Max(90, TextRenderer.MeasureText(cancel.Text, cancel.Font).Width + 28);
                int right = form.ClientSize.Width - 22;
                ok.SetBounds(right - okWidth, 170, okWidth, 34);
                cancel.SetBounds(ok.Left - 8 - cancelWidth, 170, cancelWidth, 34);
                cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(cancel);
                ok.DialogResult = DialogResult.OK;
                form.Controls.Add(ok);

                form.AcceptButton = ok;
                form.CancelButton = cancel;
                Localization.Apply(form);
                return form.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }

    internal sealed class RestoreFailureDialog : Form
    {
        public RestoreFailureAction Action = RestoreFailureAction.Cancel;

        public RestoreFailureDialog(string message)
        {
            Text = "更新设置恢复失败";
            Font = AppTheme.BodyFont;
            message = Localization.T(message);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(520, 280);
            BackColor = AppTheme.WindowBackground;
            Load += delegate { UiDpi.Apply(this); };

            Label title = AppTheme.CreateLabel("系统更新设置尚未恢复", AppTheme.TitleFont, AppTheme.Danger);
            title.SetBounds(24, 20, 470, 40);
            Controls.Add(title);

            Label body = AppTheme.CreateLabel(message, AppTheme.BodyFont, AppTheme.TextSecondary);
            body.SetBounds(24, 70, 470, 120);
            Controls.Add(body);

            Button retry = UiLayout.PrimaryButton("重试恢复");
            retry.SetBounds(150, 220, 100, 36);
            retry.Click += delegate { Action = RestoreFailureAction.Retry; Close(); };
            Controls.Add(retry);

            Button exit = UiLayout.SecondaryButton("仍然退出");
            exit.SetBounds(260, 220, 100, 36);
            exit.Click += delegate { Action = RestoreFailureAction.ExitWithoutRestore; Close(); };
            Controls.Add(exit);

            Button cancel = UiLayout.SecondaryButton("取消");
            cancel.SetBounds(370, 220, 100, 36);
            cancel.Click += delegate { Action = RestoreFailureAction.Cancel; Close(); };
            Controls.Add(cancel);
            CancelButton = cancel;
            Localization.Apply(this);
        }
    }

    internal sealed class PreviewForm : Form
    {
        private readonly DisplayInfo _display;

        public PreviewForm(DisplayInfo display, Cursor cursor)
        {
            _display = display;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = false;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Text = string.Empty;
            Bounds = new Rectangle(display.Bounds.Left, display.Bounds.Top, display.Bounds.Width, display.Bounds.Height);
            Cursor = cursor;
            DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                cp.ExStyle &= ~Native.WS_EX_APPWINDOW;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                Native.SetWindowPos(
                    Handle,
                    Native.HWND_TOPMOST,
                    _display.Bounds.Left,
                    _display.Bounds.Top,
                    _display.Bounds.Width,
                    _display.Bounds.Height,
                    Native.SWP_SHOWWINDOW);
                int corner = Native.DWMWCP_DONOTROUND;
                Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                Native.DwmFlush();
            }
            catch
            {
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
        }
    }

    internal static class PreviewOverlay
    {
        private static Timer _timer;
        private static System.Collections.Generic.List<Form> _forms;
        private static System.Collections.Generic.List<BacklightState> _backlightStates;
        private static AudioMuteService _audio;

        public static void Show(IWin32Window owner, int seconds, Settings settings)
        {
            Finish();

            System.Collections.Generic.List<Form> forms = new System.Collections.Generic.List<Form>();
            System.Collections.Generic.List<DisplayInfo> displays = DisplayManager.Enumerate();

            // Match the real blackout: dim the backlights before showing the
            // black layer. The overlay itself does not change luminance.
            if (settings != null &&
                settings.TryPhysicalOff &&
                SystemPower.HasBattery &&
                !settings.LaptopPhysicalOff &&
                settings.DimExternalBacklight)
            {
                _backlightStates = DdcBacklight.DimExternal();
            }

            Cursor cursor = TransparentCursorFactory.Get();
            for (int i = 0; i < displays.Count; i++)
            {
                PreviewForm form = new PreviewForm(displays[i], cursor);
                forms.Add(form);
                form.Show(owner);
            }

            _forms = forms;

            if (settings != null && settings.MuteSystemAudio)
            {
                try
                {
                    _audio = new AudioMuteService(settings);
                    _audio.MuteForBlackout();
                }
                catch (Exception ex)
                {
                    Log.Exception("预览静音失败", ex);
                }
            }

            _timer = new Timer();
            _timer.Interval = Math.Max(1, seconds) * 1000;
            _timer.Tick += delegate { Finish(); };
            _timer.Start();
        }

        private static void Finish()
        {
            if (_timer != null)
            {
                try { _timer.Stop(); } catch { }
                try { _timer.Dispose(); } catch { }
                _timer = null;
            }

            System.Collections.Generic.List<Form> forms = _forms;
            _forms = null;
            if (forms != null)
            {
                for (int i = 0; i < forms.Count; i++)
                {
                    try { forms[i].Close(); forms[i].Dispose(); } catch { }
                }
            }

            System.Collections.Generic.List<BacklightState> backlight = _backlightStates;
            _backlightStates = null;
            if (backlight != null && backlight.Count > 0)
            {
                try { DdcBacklight.Restore(backlight); } catch { }
            }

            AudioMuteService audio = _audio;
            _audio = null;
            if (audio != null)
            {
                try { audio.Restore(); } catch { }
            }
        }
    }
}
