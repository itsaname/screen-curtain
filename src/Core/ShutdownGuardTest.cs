using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class ShutdownGuardTestForm : Form
    {
        private readonly StringBuilder _result = new StringBuilder();

        public ShutdownGuardTestForm()
        {
            Text = "幕帘关机否决测试";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(360, 120);
            ShowInTaskbar = true;
            WindowState = FormWindowState.Minimized;
            Shown += delegate
            {
                BeginInvoke(new Action(RunTest));
            };
        }

        public static void Run()
        {
            Application.Run(new ShutdownGuardTestForm());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_QUERYENDSESSION)
            {
                uint flags = unchecked((uint)m.LParam.ToInt64());
                bool blocked = ShutdownGuard.HandleQueryEndSession(Handle, flags);
                m.Result = blocked ? IntPtr.Zero : new IntPtr(1);
                return;
            }
            base.WndProc(ref m);
        }

        private void RunTest()
        {
            bool passed = false;
            try
            {
                _result.AppendLine("ScreenCurtain shutdown guard API test");
                _result.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                ShutdownGuard.Active = true;
                ShutdownGuard.AllowShutdown = false;

                bool visible = Native.IsWindowVisible(Handle);
                _result.AppendLine("Minimized window visible: " + visible);

                IntPtr normal = Native.SendMessage(Handle, (uint)Native.WM_QUERYENDSESSION, IntPtr.Zero, IntPtr.Zero);
                _result.AppendLine("Normal query result: " + normal.ToInt64());
                _result.AppendLine("ShutdownBlockReasonCreate succeeded: " + ShutdownGuard.LastReasonCreated);

                IntPtr logoff = Native.SendMessage(
                    Handle,
                    (uint)Native.WM_QUERYENDSESSION,
                    IntPtr.Zero,
                    new IntPtr(unchecked((int)Native.ENDSESSION_LOGOFF)));
                _result.AppendLine("Logoff query result: " + logoff.ToInt64());

                passed = visible &&
                         normal == IntPtr.Zero &&
                         ShutdownGuard.LastReasonCreated &&
                         logoff == new IntPtr(1);
            }
            catch (Exception ex)
            {
                _result.AppendLine("EXCEPTION: " + ex);
            }
            finally
            {
                ShutdownGuard.Active = false;
                try { ShutdownGuard.Clear(Handle); } catch { }
            }

            _result.AppendLine("RESULT: " + (passed ? "PASS" : "FAIL"));
            string path = Environment.GetEnvironmentVariable("SCREENCURTAIN_RESULT_FILE");
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-shutdown-guard-test.txt");
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
}
