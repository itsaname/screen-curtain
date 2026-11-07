using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class DryRun
    {
        public static void Run()
        {
            List<DisplayInfo> displays = DisplayManager.Enumerate();
            if (displays.Count == 0)
            {
                MessageBox.Show("没有检测到活动显示器。", "幕帘 DRY RUN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<Form> overlays = new List<Form>();
            Form coordinator = new Form();
            coordinator.FormBorderStyle = FormBorderStyle.None;
            coordinator.ShowInTaskbar = false;
            coordinator.StartPosition = FormStartPosition.Manual;
            coordinator.Bounds = new Rectangle(-32000, -32000, 1, 1);

            Timer timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += delegate
            {
                timer.Stop();
                for (int i = 0; i < overlays.Count; i++)
                {
                    try { overlays[i].Close(); overlays[i].Dispose(); } catch { }
                }
                overlays.Clear();
                coordinator.Close();
            };

            coordinator.Shown += delegate
            {
                try
                {
                    for (int i = 0; i < displays.Count; i++)
                    {
                        DisplayInfo display = displays[i];
                        Form form = new Form();
                        form.FormBorderStyle = FormBorderStyle.None;
                        form.ShowInTaskbar = false;
                        form.TopMost = true;
                        form.StartPosition = FormStartPosition.Manual;
                        form.BackColor = Color.FromArgb(12, 12, 12);
                        form.Bounds = new Rectangle(display.Bounds.Left, display.Bounds.Top, display.Bounds.Width, display.Bounds.Height);

                        Label label = new Label();
                        label.Dock = DockStyle.Fill;
                        label.TextAlign = ContentAlignment.MiddleCenter;
                        label.ForeColor = Color.White;
                        label.BackColor = Color.Transparent;
                        label.Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold);
                        label.Text = "幕帘 DRY RUN\r\n\r\n显示器 " + (i + 1) + " / " + displays.Count +
                                     "\r\n" + display.Bounds.Width + " × " + display.Bounds.Height +
                                     "\r\n\r\n不会关闭物理屏幕，5 秒后自动恢复";
                        form.Controls.Add(label);
                        form.Show();
                        Native.SetWindowPos(form.Handle, Native.HWND_TOPMOST, display.Bounds.Left, display.Bounds.Top,
                            display.Bounds.Width, display.Bounds.Height, Native.SWP_SHOWWINDOW);
                        overlays.Add(form);
                    }

                    timer.Start();
                    Log.Info("DRY RUN：覆盖 {0} 台显示器，5 秒后自动恢复", displays.Count);
                }
                catch (Exception ex)
                {
                    Log.Exception("DRY RUN 失败", ex);
                    timer.Stop();
                    for (int i = 0; i < overlays.Count; i++)
                    {
                        try { overlays[i].Close(); overlays[i].Dispose(); } catch { }
                    }
                    overlays.Clear();
                    coordinator.Close();
                    MessageBox.Show("DRY RUN 失败：" + ex.Message, "幕帘 DRY RUN", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Application.Run(coordinator);
        }
    }
}
