using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class BlackoutTestHarness : Form
    {
        private readonly Settings _settings;
        private readonly BlackoutController _controller;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly StringBuilder _result = new StringBuilder();
        private int _step;
        private bool _failed;

        public BlackoutTestHarness()
        {
            _settings = new Settings();
            _settings.PreventSleep = true;
            _settings.AllowWheelWake = false;
            _settings.GuardEnabled = false;
            _settings.FirstRun = false;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-32000, -32000, 1, 1);
            Text = "幕帘黑屏测试";
            _controller = new BlackoutController(this, _settings);
            _controller.SuppressMonitorPower = true;
            ShutdownGuard.Active = false;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 100;
            _timer.Tick += Tick;
            Shown += delegate
            {
                _clock.Start();
                _result.AppendLine("ScreenCurtain blackout integration test");
                _result.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                bool entered = _controller.Enter();
                _result.AppendLine("Enter at t=0: " + entered + ", state=" + _controller.State);
                if (!entered)
                {
                    _failed = true;
                }
                _timer.Start();
            };
        }

        public static void Run()
        {
            Application.Run(new BlackoutTestHarness());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_APP_WAKE)
            {
                _controller.OnWakeMessage(m.WParam.ToInt32());
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void Tick(object sender, EventArgs e)
        {
            long ms = _clock.ElapsedMilliseconds;
            try
            {
                if (_step == 0 && ms >= 1300)
                {
                    _result.AppendLine("t=" + ms + " state before mouse move: " + _controller.State);
                    if (_controller.State != BlackoutState.Dark) _failed = true;
                    TestInput.MoveMouse();
                    _step = 1;
                }
                else if (_step == 1 && ms >= 1900)
                {
                    _result.AppendLine("t=" + ms + " state after mouse move: " + _controller.State);
                    if (_controller.State != BlackoutState.Dark) _failed = true;
                    TestInput.LeftClick();
                    _step = 2;
                }
                else if (_step == 2 && ms >= 2700)
                {
                    _result.AppendLine("t=" + ms + " state after left click: " + _controller.State);
                    if (_controller.State != BlackoutState.Idle) _failed = true;
                    bool entered = _controller.Enter();
                    _result.AppendLine("second Enter: " + entered + ", state=" + _controller.State);
                    if (!entered) _failed = true;
                    _step = 3;
                }
                else if (_step == 3 && ms >= 4000)
                {
                    _result.AppendLine("t=" + ms + " state before key press: " + _controller.State);
                    if (_controller.State != BlackoutState.Dark) _failed = true;
                    TestInput.PressF24();
                    _step = 4;
                }
                else if (_step == 4 && ms >= 4800)
                {
                    _result.AppendLine("t=" + ms + " state after key press: " + _controller.State);
                    if (_controller.State != BlackoutState.Idle) _failed = true;
                    Finish();
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                _result.AppendLine("EXCEPTION: " + ex);
                Finish();
            }
        }

        private void Finish()
        {
            _timer.Stop();
            _result.AppendLine("RESULT: " + (_failed ? "FAIL" : "PASS"));
            try
            {
                if (_controller.State != BlackoutState.Idle)
                {
                    _controller.SafeRestore("测试结束");
                }
                _controller.Dispose();
            }
            catch (Exception ex)
            {
                _result.AppendLine("cleanup exception: " + ex);
            }

            string path = Environment.GetEnvironmentVariable("SCREENCURTAIN_RESULT_FILE");
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-blackout-test.txt");
            }
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, _result.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }

            Close();
        }
    }

    internal static class TestInput
    {
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_F24 = 0x87;

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public static void MoveMouse()
        {
            SetCursorPos(100, 100);
            mouse_event(MOUSEEVENTF_MOVE, 2, 0, 0, UIntPtr.Zero);
        }

        public static void LeftClick()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        public static void PressF24()
        {
            keybd_event(VK_F24, 0, 0, UIntPtr.Zero);
            keybd_event(VK_F24, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
