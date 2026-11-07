using System;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class AppMessages
    {
        public const string ControlWindowTitle = "ScreenCurtain.Control";
        public const int WM_APP_ACTIVATE = 0x8010;
        public const int WM_APP_REQUEST_EXIT = 0x8011;

        public static bool ActivateExisting()
        {
            IntPtr hwnd = Native.FindWindow(null, ControlWindowTitle);
            if (hwnd == IntPtr.Zero) return false;
            Native.PostMessage(hwnd, (uint)WM_APP_ACTIVATE, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        public static bool RequestExistingExit()
        {
            IntPtr hwnd = Native.FindWindow(null, ControlWindowTitle);
            if (hwnd == IntPtr.Zero) return false;
            Native.PostMessage(hwnd, (uint)WM_APP_REQUEST_EXIT, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
    }

    internal sealed class AppControlWindow : Form
    {
        private readonly AppHost _host;

        public AppControlWindow(AppHost host)
        {
            _host = host;
            Text = AppMessages.ControlWindowTitle;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new System.Drawing.Rectangle(-32000, -32000, 1, 1);
            Opacity = 0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == AppMessages.WM_APP_ACTIVATE)
            {
                _host.Activate();
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == AppMessages.WM_APP_REQUEST_EXIT)
            {
                _host.ExitApplication();
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
