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
            AppTheme.ApplyToForm(form);
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
        private static bool _dark;

        public static bool IsDark { get { return _dark; } }

        public static Color WindowBackground
        {
            get { return _dark ? Color.FromArgb(31, 31, 31) : Color.FromArgb(243, 243, 243); }
        }

        public static Color Surface
        {
            get { return _dark ? Color.FromArgb(43, 43, 43) : Color.White; }
        }

        public static Color SurfaceMuted
        {
            get { return _dark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(248, 248, 248); }
        }

        public static Color TextPrimary
        {
            get { return _dark ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27); }
        }

        public static Color TextSecondary
        {
            get { return _dark ? Color.FromArgb(184, 184, 184) : Color.FromArgb(95, 95, 95); }
        }

        public static Color Border
        {
            get { return _dark ? Color.FromArgb(64, 64, 64) : Color.FromArgb(225, 225, 225); }
        }

        public static Color BorderStrong
        {
            get { return _dark ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200); }
        }

        public static Color Accent
        {
            get { return _dark ? Color.FromArgb(76, 163, 224) : Color.FromArgb(15, 108, 189); }
        }

        public static Color AccentHover
        {
            get { return _dark ? Color.FromArgb(104, 181, 234) : Color.FromArgb(12, 88, 154); }
        }

        public static Color AccentSoft
        {
            get { return _dark ? Color.FromArgb(35, 61, 82) : Color.FromArgb(229, 241, 251); }
        }

        public static Color AccentBorder
        {
            get { return _dark ? Color.FromArgb(63, 110, 145) : Color.FromArgb(180, 215, 245); }
        }

        public static Color Success
        {
            get { return _dark ? Color.FromArgb(108, 203, 108) : Color.FromArgb(16, 124, 16); }
        }

        public static Color Warning
        {
            get { return _dark ? Color.FromArgb(224, 169, 74) : Color.FromArgb(157, 93, 0); }
        }

        public static Color Danger
        {
            get { return _dark ? Color.FromArgb(240, 106, 94) : Color.FromArgb(196, 43, 28); }
        }

        public static Color SuccessSoft
        {
            get { return _dark ? Color.FromArgb(32, 54, 36) : Color.FromArgb(232, 246, 232); }
        }

        public static Color WarningSoft
        {
            get { return _dark ? Color.FromArgb(58, 46, 30) : Color.FromArgb(255, 247, 230); }
        }

        public static Color DangerSoft
        {
            get { return _dark ? Color.FromArgb(58, 34, 32) : Color.FromArgb(253, 235, 233); }
        }

        public static Color NeutralSoft
        {
            get { return _dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(240, 240, 240); }
        }

        public static Color TextOnAccent
        {
            get { return Color.White; }
        }

        public static void SetTheme(string theme)
        {
            string code = (theme ?? "auto").Trim().ToLowerInvariant();
            if (code == "dark")
            {
                _dark = true;
            }
            else if (code == "light")
            {
                _dark = false;
            }
            else
            {
                _dark = false;
                try
                {
                    object value = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "AppsUseLightTheme", 1);
                    if (value is int) _dark = (int)value == 0;
                }
                catch
                {
                    _dark = false;
                }
            }
        }

        public static void ApplyToForm(Form form)
        {
            if (form == null || form.IsDisposed) return;
            form.BackColor = WindowBackground;
            try
            {
                if (form.IsHandleCreated)
                {
                    int dark = _dark ? 1 : 0;
                    int result = Native.DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                    if (result != 0)
                    {
                        Native.DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
                    }
                }
            }
            catch
            {
            }
        }

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

        public static void ApplyNativeTheme(Control control)
        {
            if (control == null) return;
            try
            {
                if (control.IsHandleCreated)
                {
                    Native.SetWindowTheme(control.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null);
                }
                else
                {
                    control.HandleCreated += delegate
                    {
                        try { Native.SetWindowTheme(control.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null); }
                        catch { }
                    };
                }
            }
            catch
            {
            }
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
            Color track = on ? AppTheme.Accent : AppTheme.BorderStrong;
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
            BackColor = AppTheme.Surface;
            ForeColor = AppTheme.TextPrimary;
            AppTheme.ApplyNativeTheme(this);
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
            BackColor = AppTheme.AccentSoft;
            SelectAll();
        }

        private void EndCapture()
        {
            _recording = false;
            BackColor = AppTheme.Surface;
            if (_spec != null) Text = _spec.Text;
        }
    }

    internal sealed class DarkSelect : Button
    {
        private readonly string[] _items;
        private int _selectedIndex;
        private ContextMenuStrip _menu;

        public event EventHandler SelectionChanged;

        public DarkSelect(string[] items, int selectedIndex)
        {
            _items = items ?? new string[0];
            _selectedIndex = Math.Max(0, Math.Min(_items.Length - 1, selectedIndex));
            AppTheme.StyleSecondary(this);
            TextAlign = ContentAlignment.MiddleLeft;
            FlatAppearance.BorderColor = AppTheme.BorderStrong;
            Height = 30;
            UpdateText();
            Click += delegate { ShowMenu(); };
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int next = Math.Max(0, Math.Min(_items.Length - 1, value));
                if (next == _selectedIndex) return;
                _selectedIndex = next;
                UpdateText();
                EventHandler handler = SelectionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private void UpdateText()
        {
            Text = (_items.Length == 0 ? string.Empty : _items[_selectedIndex]) + "    ▾";
        }

        private void ShowMenu()
        {
            if (_menu != null)
            {
                try { _menu.Dispose(); } catch { }
                _menu = null;
            }
            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.BackColor = AppTheme.Surface;
            _menu.ForeColor = AppTheme.TextPrimary;
            _menu.Renderer = new DarkMenuRenderer();
            for (int i = 0; i < _items.Length; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.ForeColor = AppTheme.TextPrimary;
                item.BackColor = AppTheme.Surface;
                item.Click += delegate { SelectedIndex = index; };
                _menu.Items.Add(item);
            }
            _menu.Show(this, new Point(0, Height));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _menu != null)
            {
                try { _menu.Dispose(); } catch { }
                _menu = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class DarkMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(AppTheme.Surface))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            bool selected = (e.Item.Selected || e.Item.Pressed);
            Color color = selected ? AppTheme.AccentSoft : AppTheme.Surface;
            using (SolidBrush brush = new SolidBrush(color))
            {
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? AppTheme.TextPrimary : AppTheme.TextSecondary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(AppTheme.Border))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 6, y);
            }
        }
    }

    internal static class UiLayout
    {        public static Button PrimaryButton(string text)
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
