using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCurtain.Setup
{
    internal static class SetupProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SetupLang.Initialize(args);
            SetupOptions options = SetupOptions.Parse(args);
            SetupLog.Initialize(options.LogPath);
            SetupLog.Write("安装器启动；静默={0}，目录={1}", options.Silent, options.InstallDir);

            if (options.Silent)
            {
                try
                {
                    SetupEngine.Install(options, null);
                    SetupLog.Write("静默安装完成");
                    return 0;
                }
                catch (Exception ex)
                {
                    SetupLog.Write("静默安装失败: {0}", ex);
                    return 2;
                }
            }

            using (SetupForm form = new SetupForm(options))
            {
                Application.Run(form);
            }
            return Environment.ExitCode;
        }
    }

    internal static class SetupEngine
    {
        public static void Install(SetupOptions options, Action<string> progress)
        {
            string installDir = Path.GetFullPath(options.InstallDir);
            if (string.IsNullOrWhiteSpace(installDir) ||
                string.Equals(installDir, Path.GetPathRoot(installDir), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(SetupLang.T("安装目录无效。", "Invalid installation directory."));
            }

            if (progress != null) progress(SetupLang.T("检查目录…", "Checking the folder…"));
            Directory.CreateDirectory(installDir);
            string probe = Path.Combine(installDir, ".sc-write-test");
            File.WriteAllText(probe, "test");
            File.Delete(probe);

            string appPath = Path.Combine(installDir, SetupInfo.AppExe);
            if (progress != null) progress(SetupLang.T("关闭正在运行的幕帘…", "Closing a running ScreenCurtain…"));
            if (!ProcessHelper.StopRunningApp(appPath))
            {
                throw new InvalidOperationException(SetupLang.T("幕帘仍在运行。请先退出幕帘，再重试安装。", "ScreenCurtain is still running. Exit it and retry the installation."));
            }

            string staging = Path.Combine(Path.GetTempPath(), "ScreenCurtainStage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                if (progress != null) progress(SetupLang.T("准备安装文件…", "Preparing installation files…"));
                Payload.Extract("Payload.ScreenCurtain.exe", Path.Combine(staging, SetupInfo.AppExe));
                Payload.Extract("Payload.ScreenCurtain.exe.config", Path.Combine(staging, SetupInfo.AppConfig));
                Payload.Extract("Payload.Uninstall.exe", Path.Combine(staging, SetupInfo.UninstallerExe));
                Payload.Extract("Payload.README.md", Path.Combine(staging, "README.md"));

                string[] files = Directory.GetFiles(staging);
                for (int i = 0; i < files.Length; i++)
                {
                    string destination = Path.Combine(installDir, Path.GetFileName(files[i]));
                    File.Copy(files[i], destination, true);
                }

                if (!options.NoShortcuts)
                {
                    if (progress != null) progress(SetupLang.T("创建快捷方式…", "Creating shortcuts…"));
                    ShellIntegration.CreateShortcuts(installDir, !options.NoDesktop);
                }
                if (!options.NoArp)
                {
                    if (progress != null) progress(SetupLang.T("注册应用与功能…", "Registering in Apps & features…"));
                    InstallRegistry.Register(installDir);
                }

                SetupLog.Write("安装完成：{0}", installDir);
                if (!options.NoRun)
                {
                    if (progress != null) progress(SetupLang.T("启动幕帘…", "Starting ScreenCurtain…"));
                    ProcessHelper.Launch(appPath);
                }
            }
            catch
            {
                try { Directory.Delete(staging, true); } catch { }
                throw;
            }
            finally
            {
                try { Directory.Delete(staging, true); } catch { }
            }
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly SetupOptions _options;
        private readonly TextBox _path;
        private readonly CheckBox _desktop;
        private readonly CheckBox _run;
        private readonly Button _install;
        private readonly Button _cancel;
        private readonly Label _status;
        private bool _busy;

        public SetupForm(SetupOptions options)
        {
            _options = options;
            Text = SetupLang.T("安装幕帘", "Install ScreenCurtain");
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(580, 430);
            BackColor = Color.FromArgb(243, 243, 243);
            Load += delegate { SetupDpi.Apply(this); };
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label title = new Label();
            title.Text = SetupLang.T("安装幕帘", "Install ScreenCurtain");
            title.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
            title.AutoSize = false;
            title.SetBounds(24, 18, 420, 40);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = SetupLang.T("按用户安装，不需要管理员权限。更新保护只在运行时请求 UAC。",
                "Per-user install; no admin rights. Update protection requests UAC only while running.");
            subtitle.Font = new Font("Microsoft YaHei UI", 8.5F);
            subtitle.ForeColor = Color.FromArgb(95, 95, 95);
            subtitle.AutoSize = false;
            subtitle.SetBounds(26, 58, 520, 22);
            Controls.Add(subtitle);

            Panel card = new Panel();
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.FixedSingle;
            card.SetBounds(22, 94, 536, 236);
            Controls.Add(card);

            Label pathLabel = new Label();
            pathLabel.Text = SetupLang.T("安装位置", "Install location");
            pathLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            pathLabel.AutoSize = false;
            pathLabel.SetBounds(18, 18, 140, 22);
            card.Controls.Add(pathLabel);

            _path = new TextBox();
            _path.Text = options.InstallDir;
            _path.SetBounds(18, 44, 410, 26);
            _path.SelectionStart = 0;
            _path.SelectionLength = 0;
            card.Controls.Add(_path);

            Button browse = new Button();
            browse.Text = SetupLang.T("浏览…", "Browse…");
            browse.SetBounds(438, 42, 74, 28);
            browse.FlatStyle = FlatStyle.Flat;
            browse.Click += delegate
            {
                if (_busy) return;
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = SetupLang.T("选择幕帘安装目录", "Choose the ScreenCurtain installation folder");
                    dialog.SelectedPath = _path.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.SelectedPath;
                }
            };
            card.Controls.Add(browse);

            Label note = new Label();
            note.Text = SetupLang.T("创建开始菜单入口和卸载项；不修改系统更新设置。",
                "Creates Start menu and uninstall entries; does not modify Windows Update settings.");
            note.Font = new Font("Microsoft YaHei UI", 8.5F);
            note.ForeColor = Color.FromArgb(95, 95, 95);
            note.AutoSize = false;
            note.SetBounds(18, 82, 494, 42);
            card.Controls.Add(note);

            _desktop = new CheckBox();
            _desktop.Text = SetupLang.T("创建桌面快捷方式", "Create a desktop shortcut");
            _desktop.Checked = !options.NoDesktop;
            _desktop.BackColor = Color.White;
            _desktop.SetBounds(18, 136, 280, 26);
            card.Controls.Add(_desktop);

            _run = new CheckBox();
            _run.Text = SetupLang.T("安装完成后启动幕帘", "Launch ScreenCurtain after installation");
            _run.Checked = !options.NoRun;
            _run.BackColor = Color.White;
            _run.SetBounds(18, 166, 320, 26);
            card.Controls.Add(_run);

            _status = new Label();
            _status.Text = SetupLang.T("准备安装", "Ready to install");
            _status.Font = new Font("Microsoft YaHei UI", 8.5F);
            _status.ForeColor = Color.FromArgb(95, 95, 95);
            _status.AutoSize = false;
            _status.SetBounds(18, 202, 490, 24);
            card.Controls.Add(_status);

            _cancel = new Button();
            _cancel.Text = SetupLang.T("取消", "Cancel");
            _cancel.SetBounds(382, 360, 80, 36);
            _cancel.FlatStyle = FlatStyle.Flat;
            _cancel.BackColor = Color.White;
            _cancel.Click += delegate { if (!_busy) Close(); };
            Controls.Add(_cancel);

            _install = new Button();
            _install.Text = SetupLang.T("安装", "Install");
            _install.SetBounds(470, 360, 88, 38);
            _install.FlatStyle = FlatStyle.Flat;
            _install.FlatAppearance.BorderSize = 0;
            _install.BackColor = Color.FromArgb(15, 108, 189);
            _install.ForeColor = Color.White;
            _install.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            _install.Click += delegate { StartInstall(); };
            Controls.Add(_install);

            Label footer = new Label();
            footer.Text = "ScreenCurtain " + SetupInfo.Version;
            footer.Font = new Font("Microsoft YaHei UI", 8F);
            footer.ForeColor = Color.FromArgb(130, 130, 130);
            footer.AutoSize = false;
            footer.SetBounds(24, 366, 300, 20);
            Controls.Add(footer);

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_busy)
                {
                    e.Cancel = true;
                }
            };
            AcceptButton = _install;
            CancelButton = _cancel;
        }

        private void StartInstall()
        {
            if (_busy) return;
            _options.InstallDir = _path.Text.Trim();
            _options.NoDesktop = !_desktop.Checked;
            _options.NoRun = !_run.Checked;
            _busy = true;
            _install.Enabled = false;
            _cancel.Enabled = false;
            _path.Enabled = false;
            _desktop.Enabled = false;
            _run.Enabled = false;
            _status.Text = SetupLang.T("正在安装…", "Installing…");
            Application.DoEvents();

            try
            {
                SetupEngine.Install(_options, delegate(string text)
                {
                    _status.Text = text;
                    Application.DoEvents();
                });
                _busy = false;
                _status.Text = SetupLang.T("安装完成", "Installation complete");
                MessageBox.Show(this, SetupLang.T("幕帘已安装完成。\n\n安装位置：", "ScreenCurtain is installed.\n\nLocation: ") + _options.InstallDir,
                    SetupLang.T("安装幕帘", "Install ScreenCurtain"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                SetupLog.Write("安装失败: {0}", ex);
                _busy = false;
                _install.Enabled = true;
                _cancel.Enabled = true;
                _path.Enabled = true;
                _desktop.Enabled = true;
                _run.Enabled = true;
                _status.Text = SetupLang.T("安装失败", "Installation failed");
                MessageBox.Show(this, SetupLang.T("安装失败：\n", "Installation failed:\n") + ex.Message + SetupLang.T("\n\n日志：", "\n\nLog: ") + _options.LogPath,
                    SetupLang.T("幕帘", "ScreenCurtain"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
