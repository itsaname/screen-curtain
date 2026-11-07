using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ScreenCurtain
{
    /// <summary>
    /// Writes a crash report to a local path that does not depend on the
    /// portable data folder. This matters when the portable folder is on a
    /// network/synced share that is slow, locked or unavailable.
    /// </summary>
    internal static class CrashLog
    {
        public static string Write(string reason, string detail)
        {
            string path = null;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenCurtain",
                    "logs");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".log");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("ScreenCurtain crash report");
                sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                sb.AppendLine("Reason: " + (reason ?? "unknown"));
                sb.AppendLine("Version: " + ReleaseInfo.FullVersion);
                sb.AppendLine("OS: " + Environment.OSVersion.VersionString);
                sb.AppendLine("Process: " + Environment.Version + (Environment.Is64BitProcess ? " x64" : " x86"));
                sb.AppendLine("Data directory: " + Safe(AppPaths.DataDirectory));
                sb.AppendLine("Log directory: " + Safe(AppPaths.LogDirectory));
                sb.AppendLine("Last log: " + Safe(Log.LastMessage));
                sb.AppendLine();
                sb.AppendLine(detail ?? string.Empty);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                path = null;
            }

            // Also try to leave a copy in the portable data logs. This is
            // best-effort: a broken network share must not delay termination.
            try
            {
                if (path != null && Directory.Exists(AppPaths.LogDirectory))
                {
                    string copy = Path.Combine(AppPaths.LogDirectory, Path.GetFileName(path));
                    Thread copier = new Thread(delegate()
                    {
                        try { File.Copy(path, copy, true); } catch { }
                    });
                    copier.IsBackground = true;
                    copier.Name = "ScreenCurtain.CrashCopy";
                    copier.Start();
                    copier.Join(750);
                }
            }
            catch
            {
            }

            return path;
        }

        public static string WriteException(string reason, Exception ex)
        {
            return Write(reason, ex == null ? "(no exception object)" : ex.ToString());
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "(unknown)" : value;
        }
    }
}
