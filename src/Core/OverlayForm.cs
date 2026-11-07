using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal enum InputSource
    {
        MouseButton,
        MouseWheel,
        Keyboard
    }

    internal sealed class OverlayForm : Form
    {
        private readonly BlackoutController _controller;
        private readonly DisplayInfo _display;

        public OverlayForm(BlackoutController controller, DisplayInfo display, Cursor cursor)
        {
            _controller = controller;
            _display = display;

            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Text = string.Empty;
            Bounds = new Rectangle(display.Bounds.Left, display.Bounds.Top, display.Bounds.Width, display.Bounds.Height);
            Cursor = cursor;
            DoubleBuffered = true;
        }

        public DisplayInfo Display
        {
            get { return _display; }
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
            catch (Exception ex)
            {
                Log.Exception("OverlayForm.OnShown", ex);
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _controller.OnOverlayInput(InputSource.MouseButton, true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _controller.OnOverlayInput(InputSource.MouseButton, false);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _controller.OnOverlayInput(InputSource.MouseWheel, true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            e.Handled = true;
            e.SuppressKeyPress = true;
            _controller.OnOverlayInput(InputSource.Keyboard, true);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            e.Handled = true;
            e.SuppressKeyPress = true;
            _controller.OnOverlayInput(InputSource.Keyboard, false);
        }

        public void FocusForInput()
        {
            try
            {
                Activate();
                Focus();
                Native.SetForegroundWindow(Handle);
            }
            catch (Exception ex)
            {
                Log.Exception("聚焦覆盖窗口失败", ex);
            }
        }
    }
}
