using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ScreenCurtain
{
    internal static class Log
    {
        private static readonly object Sync = new object();

        public static string DirectoryPath
        {
            get
            {
                return AppPaths.LogDirectory;
            }
        }

        public static string CurrentFile
        {
            get
            {
                return Path.Combine(DirectoryPath, "app-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
            }
        }

        public static void Info(string format, params object[] args)
        {
            Write("INFO", format, args);
        }

        public static void Warn(string format, params object[] args)
        {
            Write("WARN", format, args);
        }

        public static void Error(string format, params object[] args)
        {
            Write("ERROR", format, args);
        }

        public static void Exception(string context, Exception ex)
        {
            if (ex == null)
            {
                Write("ERROR", context, null);
                return;
            }
            Write("ERROR", context + " | " + ex.GetType().Name + ": " + ex.Message, null);
            try
            {
                WriteRaw(ex.ToString());
            }
            catch
            {
            }
        }

        private static void Write(string level, string format, object[] args)
        {
            string message;
            try
            {
                message = (args == null || args.Length == 0) ? format : string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch
            {
                message = format;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [T{2}] {3}",
                DateTime.Now,
                level,
                System.Threading.Thread.CurrentThread.ManagedThreadId,
                message);

            WriteRaw(line);
        }

        private static void WriteRaw(string line)
        {
            try
            {
                lock (Sync)
                {
                    System.IO.Directory.CreateDirectory(DirectoryPath);
                    File.AppendAllText(CurrentFile, line + Environment.NewLine, new UTF8Encoding(false));
                    TrimOldFiles();
                }
            }
            catch
            {
                // Logging must never bring down the application.
            }
        }

        private static void TrimOldFiles()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-7);
                string[] files = System.IO.Directory.GetFiles(DirectoryPath, "app-*.log");
                for (int i = 0; i < files.Length; i++)
                {
                    if (File.GetLastWriteTime(files[i]) < cutoff)
                    {
                        File.Delete(files[i]);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
