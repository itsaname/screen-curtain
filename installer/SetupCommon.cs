using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenCurtain.Setup
{
    internal static class SetupLang
    {
        public static bool English { get; private set; }

        public static void Initialize(string[] args)
        {
            English = !string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "zh",
                StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (string.Equals(arg, "/LANG=EN", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "/LANG=EN-US", StringComparison.OrdinalIgnoreCase))
                {
                    English = true;
                }
                else if (string.Equals(arg, "/LANG=ZH", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(arg, "/LANG=ZH-CN", StringComparison.OrdinalIgnoreCase))
                {
                    English = false;
                }
            }
        }

        public static string T(string chinese, string english)
        {
            return English ? english : chinese;
        }
    }
    internal static class SetupDpi
    {
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed) return;
            form.AutoScaleMode = AutoScaleMode.None;
            int dpi = 96;
            try
            {
                if (form.IsHandleCreated) dpi = (int)GetDpiForWindow(form.Handle);
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
    internal static class SetupInfo
    {
        public const string ProductName = "幕帘 ScreenCurtain";
        public const string ShortName = "幕帘";
        public const string Version = "1.3.0";
        public const string AppExe = "ScreenCurtain.exe";
        public const string AppConfig = "ScreenCurtain.exe.config";
        public const string UninstallerExe = "Uninstall.exe";
        public const string ArpKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenCurtain";
        public const string GuardBackupKey = @"SOFTWARE\ScreenCurtain\GuardBackup";

        public static string DefaultInstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "ScreenCurtain");
            }
        }

        public static string StartMenuFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    ShortName);
            }
        }

        public static string DesktopShortcut
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    ShortName + ".lnk");
            }
        }

        public static string StartMenuShortcut
        {
            get { return Path.Combine(StartMenuFolder, ShortName + ".lnk"); }
        }

        public static string StartMenuUninstallShortcut
        {
            get { return Path.Combine(StartMenuFolder, SetupLang.T("卸载幕帘", "Uninstall ScreenCurtain") + ".lnk"); }
        }

        public static string UserDataDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenCurtain");
            }
        }
    }

    internal sealed class SetupOptions
    {
        public bool Silent;
        public bool NoDesktop;
        public bool NoRun;
        public bool NoArp;
        public bool NoShortcuts;
        public string InstallDir;
        public string LogPath;

        public static SetupOptions Parse(string[] args)
        {
            SetupOptions options = new SetupOptions();
            options.InstallDir = SetupInfo.DefaultInstallDir;
            options.LogPath = Path.Combine(Path.GetTempPath(), "ScreenCurtain-Setup.log");
            if (args == null) return options;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "/SILENT", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase))
                {
                    options.Silent = true;
                }
                else if (a.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase))
                {
                    options.InstallDir = a.Substring("/DIR=".Length).Trim('"');
                }
                else if (string.Equals(a, "/NODESKTOP", StringComparison.OrdinalIgnoreCase)) options.NoDesktop = true;
                else if (string.Equals(a, "/NORUN", StringComparison.OrdinalIgnoreCase)) options.NoRun = true;
                else if (string.Equals(a, "/NOARP", StringComparison.OrdinalIgnoreCase)) options.NoArp = true;
                else if (string.Equals(a, "/NOSHORTCUTS", StringComparison.OrdinalIgnoreCase)) options.NoShortcuts = true;
                else if (a.StartsWith("/LOG=", StringComparison.OrdinalIgnoreCase)) options.LogPath = a.Substring("/LOG=".Length).Trim('"');
            }
            return options;
        }
    }

    internal static class SetupLog
    {
        private static readonly object Sync = new object();
        private static string _path;

        public static void Initialize(string path)
        {
            _path = path;
        }

        public static void Write(string format, params object[] args)
        {
            try
            {
                lock (Sync)
                {
                    if (string.IsNullOrEmpty(_path)) return;
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + string.Format(format, args) + Environment.NewLine;
                    File.AppendAllText(_path, line, new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }

    internal static class Payload
    {
        public static void Extract(string resourceName, string destination)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("安装器缺少资源：" + resourceName);
                }
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(output);
                }
            }
        }
    }

    internal static class ShortcutHelper
    {
        public static void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory, string description, string iconPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("系统未注册 WScript.Shell，无法创建快捷方式。");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments ?? string.Empty });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory ?? string.Empty });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description ?? string.Empty });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                SetupLog.Write("删除快捷方式失败 {0}: {1}", path, ex.Message);
            }
        }
    }

    internal static class ProcessHelper
    {
        public static bool StopRunningApp(string appPath)
        {
            if (!File.Exists(appPath)) return true;
            try
            {
                ProcessStartInfo request = new ProcessStartInfo();
                request.FileName = appPath;
                request.Arguments = "--request-exit";
                request.UseShellExecute = false;
                request.CreateNoWindow = true;
                Process p = Process.Start(request);
                if (p != null) p.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                SetupLog.Write("请求旧实例退出失败: {0}", ex.Message);
            }

            for (int i = 0; i < 10; i++)
            {
                bool running = false;
                Process[] processes = Process.GetProcessesByName("ScreenCurtain");
                for (int j = 0; j < processes.Length; j++)
                {
                    try
                    {
                        if (string.Equals(processes[j].MainModule.FileName, appPath, StringComparison.OrdinalIgnoreCase))
                        {
                            running = true;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
                if (!running) return true;
                System.Threading.Thread.Sleep(300);
            }
            return false;
        }

        public static void Launch(string appPath)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = appPath;
            info.WorkingDirectory = Path.GetDirectoryName(appPath);
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }

    internal static class InstallRegistry
    {
        public static void Register(string installDir)
        {
            string app = Path.Combine(installDir, SetupInfo.AppExe);
            string uninstaller = Path.Combine(installDir, SetupInfo.UninstallerExe);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SetupInfo.ArpKey))
            {
                key.SetValue("DisplayName", SetupInfo.ProductName);
                key.SetValue("DisplayVersion", SetupInfo.Version);
                key.SetValue("Publisher", "ScreenCurtain");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", app + ",0");
                key.SetValue("UninstallString", "\"" + uninstaller + "\"");
                key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" /SILENT");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("EstimatedSize", EstimateKilobytes(installDir), RegistryValueKind.DWord);
            }
        }

        public static void Unregister()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(SetupInfo.ArpKey, false); }
            catch (Exception ex) { SetupLog.Write("删除卸载项失败: {0}", ex.Message); }
            try
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (run != null) run.DeleteValue("ScreenCurtain", false);
                }
            }
            catch (Exception ex) { SetupLog.Write("删除开机自启项失败: {0}", ex.Message); }
        }

        public static bool HasGuardBackup()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SetupInfo.GuardBackupKey))
                {
                    return key != null && key.ValueCount + key.SubKeyCount > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static int EstimateKilobytes(string dir)
        {
            try
            {
                int bytes = 0;
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try { bytes += (int)new FileInfo(files[i]).Length; } catch { }
                }
                return Math.Max(1, bytes / 1024);
            }
            catch
            {
                return 1;
            }
        }
    }

    internal static class ShellIntegration
    {
        public static void CreateShortcuts(string installDir, bool desktop)
        {
            string app = Path.Combine(installDir, SetupInfo.AppExe);
            string uninstaller = Path.Combine(installDir, SetupInfo.UninstallerExe);
            ShortcutHelper.Create(SetupInfo.StartMenuShortcut, app, string.Empty, installDir, SetupLang.T("幕帘 ScreenCurtain", "ScreenCurtain"), app);
            ShortcutHelper.Create(SetupInfo.StartMenuUninstallShortcut, uninstaller, string.Empty, installDir, SetupLang.T("卸载幕帘 ScreenCurtain", "Uninstall ScreenCurtain"), app);
            if (desktop)
            {
                ShortcutHelper.Create(SetupInfo.DesktopShortcut, app, string.Empty, installDir, SetupLang.T("幕帘 ScreenCurtain", "ScreenCurtain"), app);
            }
        }

        public static void RemoveShortcuts()
        {
            ShortcutHelper.Delete(SetupInfo.StartMenuShortcut);
            ShortcutHelper.Delete(SetupInfo.StartMenuUninstallShortcut);
            ShortcutHelper.Delete(SetupInfo.DesktopShortcut);
            try
            {
                if (Directory.Exists(SetupInfo.StartMenuFolder) &&
                    Directory.GetFiles(SetupInfo.StartMenuFolder).Length == 0 &&
                    Directory.GetDirectories(SetupInfo.StartMenuFolder).Length == 0)
                {
                    Directory.Delete(SetupInfo.StartMenuFolder);
                }
            }
            catch
            {
            }
        }
    }
}

