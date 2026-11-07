using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenCurtain.Setup
{
    internal static class UninstallProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SetupLang.Initialize(args);
            bool silent = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/SILENT", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "/S", StringComparison.OrdinalIgnoreCase)) silent = true;
            }
            SetupLog.Initialize(Path.Combine(Path.GetTempPath(), "ScreenCurtain-Uninstall.log"));
            SetupLog.Write("卸载器启动；静默={0}，目录={1}", silent, AppDomain.CurrentDomain.BaseDirectory);
            UninstallEngine engine = new UninstallEngine(AppDomain.CurrentDomain.BaseDirectory);

            if (silent)
            {
                try
                {
                    engine.Uninstall(false, true);
                    return 0;
                }
                catch (Exception ex)
                {
                    SetupLog.Write("静默卸载失败: {0}", ex);
                    return 2;
                }
            }

            using (UninstallForm form = new UninstallForm(engine))
            {
                Application.Run(form);
            }
            return Environment.ExitCode;
        }
    }

    internal sealed class UninstallEngine
    {
        public readonly string InstallDir;
        public readonly string AppPath;
        public readonly string UninstallerPath;

        public UninstallEngine(string installDir)
        {
            InstallDir = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar);
            AppPath = Path.Combine(InstallDir, SetupInfo.AppExe);
            UninstallerPath = Path.Combine(InstallDir, SetupInfo.UninstallerExe);
        }

        public bool HasGuardBackup
        {
            get { return InstallRegistry.HasGuardBackup(); }
        }

        public void Uninstall(bool deleteUserData, bool silent)
        {
            StopApplication();
            if (HasGuardBackup && !RepairGuard())
            {
                throw new InvalidOperationException(SetupLang.T("未能恢复 Windows Update 设置，已停止卸载。", "Could not restore Windows Update settings; uninstallation stopped."));
            }
            Remove(deleteUserData);
        }

        public void StopApplication()
        {
            if (!ProcessHelper.StopRunningApp(AppPath))
            {
                throw new InvalidOperationException(SetupLang.T("幕帘仍在运行。请先退出幕帘，再重试卸载。", "ScreenCurtain is still running. Exit it and retry the uninstallation."));
            }
        }

        public bool RepairGuard()
        {
            if (!File.Exists(AppPath)) return true;
            if (!HasGuardBackup) return true;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = AppPath;
                info.Arguments = "--repair-update";
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.WorkingDirectory = InstallDir;
                Process p = Process.Start(info);
                if (p == null) return false;
                p.WaitForExit();
                SetupLog.Write("更新设置修复进程退出码={0}", p.ExitCode);
                return p.ExitCode == 0 && !HasGuardBackup;
            }
            catch (Win32Exception ex)
            {
                SetupLog.Write("请求管理员权限失败: {0}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                SetupLog.Write("修复更新设置失败: {0}", ex);
                return false;
            }
        }

        public void Remove(bool deleteUserData)
        {
            ValidateSafeInstall();
            ShellIntegration.RemoveShortcuts();
            InstallRegistry.Unregister();
            if (deleteUserData)
            {
                TryDeleteUserData();
            }

            ScheduleSelfDelete();
        }

        private void ValidateSafeInstall()
        {
            if (!Directory.Exists(InstallDir))
            {
                throw new InvalidOperationException(SetupLang.T("安装目录不存在。", "Installation directory does not exist."));
            }
            if (!File.Exists(Path.Combine(InstallDir, SetupInfo.AppExe)) ||
                !File.Exists(Path.Combine(InstallDir, SetupInfo.UninstallerExe)))
            {
                throw new InvalidOperationException(SetupLang.T("当前目录不包含完整的幕帘安装文件，已停止卸载以防误删。", "This folder does not contain a complete ScreenCurtain installation; uninstallation stopped to avoid deleting the wrong files."));
            }
            string root = Path.GetPathRoot(InstallDir);
            if (string.Equals(InstallDir, root, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(InstallDir, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(SetupLang.T("拒绝删除不安全的安装目录。", "Refusing to delete an unsafe installation directory."));
            }
        }

        private void TryDeleteUserData()
        {
            string data = SetupInfo.UserDataDir;
            if (!Directory.Exists(data)) return;
            string expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScreenCurtain");
            if (!string.Equals(Path.GetFullPath(data).TrimEnd(Path.DirectorySeparatorChar),
                               Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar),
                               StringComparison.OrdinalIgnoreCase))
            {
                SetupLog.Write("跳过不在预期位置的用户数据目录：{0}", data);
                return;
            }
            try
            {
                Directory.Delete(data, true);
            }
            catch (Exception ex)
            {
                SetupLog.Write("删除用户设置失败: {0}", ex.Message);
            }
        }

        private void ScheduleSelfDelete()
        {
            try
            {
                string command = "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + InstallDir + "\"";
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Environment.GetEnvironmentVariable("ComSpec");
                info.Arguments = command;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(info);
                SetupLog.Write("已安排退出后删除安装目录");
            }
            catch (Exception ex)
            {
                SetupLog.Write("安排目录删除失败: {0}", ex.Message);
            }
        }
    }

    internal sealed class UninstallForm : Form
    {
        private readonly UninstallEngine _engine;
        private readonly CheckBox _deleteData;
        private readonly Button _uninstall;
        private readonly Button _cancel;
        private readonly Label _status;
        private bool _busy;

        public UninstallForm(UninstallEngine engine)
        {
            _engine = engine;
            Text = SetupLang.T("卸载幕帘", "Uninstall ScreenCurtain");
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(520, 370);
            BackColor = Color.FromArgb(243, 243, 243);
            Load += delegate { SetupDpi.Apply(this); };
            try { Icon = Icon.ExtractAssociatedIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SetupInfo.AppExe)); } catch { }

            Label title = new Label();
            title.Text = SetupLang.T("卸载幕帘", "Uninstall ScreenCurtain");
            title.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
            title.SetBounds(24, 20, 380, 38);
            Controls.Add(title);

            Label text = new Label();
            text.Text = SetupLang.T(
                "将删除程序文件、开始菜单/桌面快捷方式和“应用与功能”卸载项。\n\n如果存在更新保护，卸载前会请求管理员权限恢复 Windows Update 原始设置。",
                "This removes the program files, Start menu/desktop shortcuts and the Apps & features entry.\n\nIf update protection exists, administrator rights are requested first to restore the original Windows Update settings.");
            text.Font = new Font("Microsoft YaHei UI", 9F);
            text.ForeColor = Color.FromArgb(70, 70, 70);
            text.SetBounds(26, 72, 462, 132);
            Controls.Add(text);

            _deleteData = new CheckBox();
            _deleteData.Text = SetupLang.T("同时删除我的幕帘设置和日志", "Also delete my ScreenCurtain settings and logs");
            _deleteData.SetBounds(26, 210, 420, 26);
            _deleteData.BackColor = Color.Transparent;
            Controls.Add(_deleteData);

            _status = new Label();
            _status.Text = SetupLang.T("准备卸载", "Ready to uninstall");
            _status.Font = new Font("Microsoft YaHei UI", 8.5F);
            _status.ForeColor = Color.FromArgb(95, 95, 95);
            _status.SetBounds(26, 242, 460, 22);
            Controls.Add(_status);

            _cancel = new Button();
            _cancel.Text = SetupLang.T("取消", "Cancel");
            _cancel.SetBounds(320, 306, 80, 36);
            _cancel.FlatStyle = FlatStyle.Flat;
            _cancel.BackColor = Color.White;
            _cancel.Click += delegate { if (!_busy) Close(); };
            Controls.Add(_cancel);

            _uninstall = new Button();
            _uninstall.Text = SetupLang.T("卸载", "Uninstall");
            _uninstall.SetBounds(410, 306, 86, 38);
            _uninstall.FlatStyle = FlatStyle.Flat;
            _uninstall.FlatAppearance.BorderSize = 0;
            _uninstall.BackColor = Color.FromArgb(196, 43, 28);
            _uninstall.ForeColor = Color.White;
            _uninstall.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            _uninstall.Click += delegate { StartUninstall(); };
            Controls.Add(_uninstall);

            Label footer = new Label();
            footer.Text = "ScreenCurtain " + SetupInfo.Version;
            footer.Font = new Font("Microsoft YaHei UI", 8F);
            footer.ForeColor = Color.FromArgb(130, 130, 130);
            footer.SetBounds(26, 314, 220, 20);
            Controls.Add(footer);

            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (_busy) e.Cancel = true; };
            AcceptButton = _uninstall;
            CancelButton = _cancel;
        }

        private void StartUninstall()
        {
            if (_busy) return;
            _busy = true;
            _uninstall.Enabled = false;
            _cancel.Enabled = false;
            _deleteData.Enabled = false;
            _status.Text = SetupLang.T("正在准备卸载…", "Preparing to uninstall…");
            Application.DoEvents();

            try
            {
                _engine.StopApplication();
                if (_engine.HasGuardBackup)
                {
                    _status.Text = SetupLang.T("正在恢复 Windows Update 设置…", "Restoring Windows Update settings…");
                    Application.DoEvents();
                    if (!_engine.RepairGuard())
                    {
                        bool continueWithoutRepair = MessageBox.Show(
                            this,
                            SetupLang.T("未能恢复 Windows Update 设置。继续卸载可能遗留临时更新策略。\n\n是否仍然卸载？",
                                "Could not restore Windows Update settings. Continuing may leave temporary update policy behind.\n\nUninstall anyway?"),
                            SetupLang.T("恢复设置失败", "Restore failed"),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                        if (!continueWithoutRepair)
                        {
                            _busy = false;
                            _uninstall.Enabled = true;
                            _cancel.Enabled = true;
                            _deleteData.Enabled = true;
                            _status.Text = SetupLang.T("已取消卸载", "Uninstallation cancelled");
                            return;
                        }
                    }
                }

                _status.Text = SetupLang.T("正在删除程序文件…", "Deleting program files…");
                Application.DoEvents();
                _engine.Remove(_deleteData.Checked);
                _status.Text = SetupLang.T("卸载完成", "Uninstallation complete");
                MessageBox.Show(this, SetupLang.T("幕帘已卸载。\n\n如果安装目录未立即消失，它会在几秒后自动删除。",
                        "ScreenCurtain is uninstalled.\n\nIf the installation folder does not disappear immediately, it will be removed in a few seconds."),
                    SetupLang.T("卸载幕帘", "Uninstall ScreenCurtain"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                SetupLog.Write("卸载失败: {0}", ex);
                _busy = false;
                _uninstall.Enabled = true;
                _cancel.Enabled = true;
                _deleteData.Enabled = true;
                _status.Text = SetupLang.T("卸载失败", "Uninstallation failed");
                MessageBox.Show(this, SetupLang.T("卸载失败：\n", "Uninstallation failed:\n") + ex.Message,
                    SetupLang.T("幕帘", "ScreenCurtain"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
