using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class UiDpi
    {
        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed) return;
            form.AutoScaleMode = AutoScaleMode.None;
            int dpi = 96;
            try
            {
                if (form.IsHandleCreated) dpi = (int)Native.GetDpiForWindow(form.Handle);
            }
            catch
            {
            }
            if (dpi <= 0) dpi = 96;
            float factor = dpi / 96f;
            if (factor < 0.5f || factor > 4f) factor = 1f;
            if (Math.Abs(factor - 1f) < 0.01f) return;

            form.SuspendLayout();
            form.Scale(new SizeF(factor, factor));
            form.ResumeLayout(false);
            form.PerformLayout();
        }
    }

    internal static class AppTheme
    {
        public static readonly Color WindowBackground = Color.FromArgb(243, 243, 243);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceMuted = Color.FromArgb(248, 248, 248);
        public static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
        public static readonly Color TextSecondary = Color.FromArgb(95, 95, 95);
        public static readonly Color Border = Color.FromArgb(225, 225, 225);
        public static readonly Color Accent = Color.FromArgb(15, 108, 189);
        public static readonly Color AccentHover = Color.FromArgb(12, 88, 154);
        public static readonly Color Success = Color.FromArgb(16, 124, 16);
        public static readonly Color Warning = Color.FromArgb(157, 93, 0);
        public static readonly Color Danger = Color.FromArgb(196, 43, 28);
        public static readonly Color SuccessSoft = Color.FromArgb(232, 246, 232);
        public static readonly Color WarningSoft = Color.FromArgb(255, 247, 230);
        public static readonly Color DangerSoft = Color.FromArgb(253, 235, 233);
        public static readonly Color NeutralSoft = Color.FromArgb(240, 240, 240);

        public static readonly Font BodyFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font SmallFont = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font SectionFont = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font TitleFont = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font HeroFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point);

        public static void StylePrimary(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.Font = new Font(BodyFont.FontFamily, 9.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Height = 38;
        }

        public static void StyleSecondary(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.BackColor = Surface;
            button.ForeColor = TextPrimary;
            button.Font = BodyFont;
            button.Cursor = Cursors.Hand;
            button.Height = 34;
        }

        public static void StyleLink(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.Transparent;
            button.ForeColor = Accent;
            button.Font = BodyFont;
            button.Cursor = Cursors.Hand;
            button.Height = 28;
        }

        public static Label CreateLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            return label;
        }
    }

    internal sealed class CardPanel : Panel
    {
        public int CornerRadius = 8;
        public Color BorderColor = AppTheme.Border;
        public Color FillColor = AppTheme.Surface;

        public CardPanel()
        {
            DoubleBuffered = true;
            BackColor = FillColor;
            Padding = new Padding(16);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRoundedRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            try
            {
                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius))
                {
                    Region previous = Region;
                    Region = new Region(path);
                    if (previous != null) previous.Dispose();
                }
            }
            catch
            {
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // The custom rounded fill must cover the whole control first;
            // otherwise the square corners expose the unpainted window
            // background and appear as black pixels.
            using (SolidBrush outside = new SolidBrush(Parent != null ? Parent.BackColor : BackColor))
            {
                e.Graphics.FillRectangle(outside, ClientRectangle);
            }

            using (GraphicsPath path = RoundedRect(rect, CornerRadius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class StatusPill : Control
    {
        public Color DotColor = AppTheme.NeutralSoft;
        public Color PillColor = AppTheme.NeutralSoft;
        public Color TextColor = AppTheme.TextSecondary;

        public StatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Font = AppTheme.SmallFont;
            Height = 28;
            Width = 160;
            AccessibleRole = AccessibleRole.StaticText;
        }

        public void SetStatus(string text, Color dot, Color pill, Color textColor)
        {
            Text = text;
            DotColor = dot;
            PillColor = pill;
            TextColor = textColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush outside = new SolidBrush(Parent != null ? Parent.BackColor : BackColor))
            {
                e.Graphics.FillRectangle(outside, ClientRectangle);
            }
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, 14))
            using (SolidBrush brush = new SolidBrush(PillColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            using (SolidBrush dot = new SolidBrush(DotColor))
            {
                e.Graphics.FillEllipse(dot, 9, Height / 2 - 4, 8, 8);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(23, 0, Width - 26, Height), TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            AutoSize = false;
            Size = new Size(42, 24);
            Text = string.Empty;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            AccessibleRole = AccessibleRole.CheckButton;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush outside = new SolidBrush(Parent != null ? Parent.BackColor : BackColor))
            {
                e.Graphics.FillRectangle(outside, ClientRectangle);
            }
            bool on = Checked;
            Color track = on ? AppTheme.Accent : Color.FromArgb(200, 200, 200);
            Rectangle trackRect = new Rectangle(0, 2, Width - 1, Height - 5);
            using (GraphicsPath path = RoundedRect(trackRect, (Height - 5) / 2))
            using (SolidBrush brush = new SolidBrush(track))
            {
                e.Graphics.FillPath(brush, path);
            }
            int knob = Height - 8;
            int x = on ? Width - knob - 4 : 4;
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(brush, x, 4, knob, knob);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class InlineBanner : Panel
    {
        private readonly Label _title;
        private readonly Label _body;
        private readonly Button _action;
        public event EventHandler ActionClicked;

        public InlineBanner()
        {
            BackColor = AppTheme.WarningSoft;
            Padding = new Padding(14, 10, 14, 10);
            _title = new Label();
            _title.AutoSize = false;
            _title.Height = 20;
            _title.Font = new Font(AppTheme.BodyFont, FontStyle.Bold);
            _title.ForeColor = AppTheme.Warning;
            _title.BackColor = Color.Transparent;
            _body = new Label();
            _body.AutoSize = false;
            _body.Font = AppTheme.SmallFont;
            _body.ForeColor = AppTheme.TextSecondary;
            _body.BackColor = Color.Transparent;
            _action = new Button();
            AppTheme.StyleLink(_action);
            _action.Text = "处理";
            _action.Visible = false;
            _action.Click += delegate { EventHandler h = ActionClicked; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_title); Controls.Add(_body); Controls.Add(_action);
        }

        public void Configure(string title, string body, string action, Color back, Color fore)
        {
            Text = title;
            _title.Text = title;
            _body.Text = body;
            BackColor = back;
            _title.ForeColor = fore;
            _action.Text = action;
            _action.Visible = !string.IsNullOrEmpty(action);
            DoLayout();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            DoLayout();
        }

        private void DoLayout()
        {
            if (_title == null) return;
            int y = Padding.Top;
            _title.SetBounds(Padding.Left, y, Width - Padding.Horizontal, 20);
            _body.SetBounds(Padding.Left, y + 22, Width - Padding.Horizontal, Math.Max(20, Height - y - 30));
            _action.SetBounds(Width - Padding.Right - 58, y - 2, 58, 24);
            PerformLayout();
        }
    }

    internal sealed class HotkeyCaptureBox : TextBox
    {
        private HotkeySpec _spec;
        private bool _recording;
        public event EventHandler HotkeyChanged;

        public HotkeyCaptureBox()
        {
            ReadOnly = true;
            Cursor = Cursors.Hand;
            TextAlign = HorizontalAlignment.Center;
            Font = new Font(AppTheme.BodyFont.FontFamily, 10F, FontStyle.Bold);
            BackColor = Color.White;
        }

        public HotkeySpec Spec
        {
            get { return _spec; }
            set
            {
                _spec = value ?? HotkeySpec.Default;
                Text = _spec.Text;
            }
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            BeginCapture();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            EndCapture();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            BeginCapture();
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.Escape)
            {
                EndCapture();
                return;
            }

            uint mods = 0;
            if (e.Control) mods |= (uint)Native.MOD_CONTROL;
            if (e.Alt) mods |= (uint)Native.MOD_ALT;
            if (e.Shift) mods |= (uint)Native.MOD_SHIFT;
            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) mods |= (uint)Native.MOD_WIN;
            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.Menu || key == Keys.ShiftKey ||
                key == Keys.LWin || key == Keys.RWin)
            {
                return;
            }
            if (mods == 0)
            {
                return;
            }
            if (key == Keys.Delete && e.Control && e.Alt)
            {
                return;
            }

            string text = HotkeySpec.ModifiersToText(mods) + "+" + key.ToString();
            HotkeySpec newSpec = HotkeySpec.Parse(text);
            if (newSpec == null) return;
            _spec = newSpec;
            Text = newSpec.Text;
            EndCapture();
            EventHandler h = HotkeyChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void BeginCapture()
        {
            if (_recording) return;
            _recording = true;
            Text = "请按下组合键…";
            BackColor = Color.FromArgb(235, 245, 255);
            SelectAll();
        }

        private void EndCapture()
        {
            _recording = false;
            BackColor = Color.White;
            if (_spec != null) Text = _spec.Text;
        }
    }

    internal static class UiLayout
    {
        public static Label SectionLabel(string text)
        {
            Label label = AppTheme.CreateLabel(text, AppTheme.SectionFont, AppTheme.TextPrimary);
            label.Height = 24;
            return label;
        }

        public static Label BodyLabel(string text)
        {
            Label label = AppTheme.CreateLabel(text, AppTheme.BodyFont, AppTheme.TextSecondary);
            label.Height = 22;
            return label;
        }

        public static Button PrimaryButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            AppTheme.StylePrimary(button);
            return button;
        }

        public static Button SecondaryButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            AppTheme.StyleSecondary(button);
            return button;
        }
    }
}
