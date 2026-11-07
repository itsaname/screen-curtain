using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ScreenCurtain
{
    internal static class Log
    {
        private static readonly object Sync = new object();
        private static readonly object FileSync = new object();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly AutoResetEvent Signal = new AutoResetEvent(false);
        private static readonly Thread Writer;

        static Log()
        {
            Writer = new Thread(WriterLoop);
            Writer.IsBackground = true;
            Writer.Name = "ScreenCurtain.Log";
            Writer.Start();
        }

        public static string LastMessage { get; private set; }

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

        /// <summary>
        /// Best-effort flush used on controlled shutdown. The writer normally
        /// drains asynchronously so a slow network share cannot block the UI
        /// thread (which previously triggered the 10-second watchdog).
        /// </summary>
        public static void Flush(int timeoutMs)
        {
            try
            {
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
                while (DateTime.UtcNow < deadline)
                {
                    lock (Sync)
                    {
                        if (Pending.Count == 0) return;
                    }
                    Thread.Sleep(20);
                }
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
                Thread.CurrentThread.ManagedThreadId,
                message);

            WriteRaw(line);
        }

        private static void WriteRaw(string line)
        {
            lock (Sync)
            {
                LastMessage = line;
                Pending.Enqueue(line);
            }
            Signal.Set();
        }

        private static void WriterLoop()
        {
            while (true)
            {
                try
                {
                    Signal.WaitOne();
                    Drain();
                }
                catch
                {
                }
            }
        }

        private static void Drain()
        {
            List<string> lines;
            lock (Sync)
            {
                if (Pending.Count == 0) return;
                lines = new List<string>(Pending);
                Pending.Clear();
            }

            lock (FileSync)
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    StringBuilder batch = new StringBuilder();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        batch.Append(lines[i]);
                        batch.Append(Environment.NewLine);
                    }
                    File.AppendAllText(CurrentFile, batch.ToString(), new UTF8Encoding(false));
                    TrimOldFiles();
                }
                catch
                {
                    // Logging must never bring down the application. On a
                    // network share the crash report is written locally by
                    // CrashLog instead.
                }
            }
        }

        private static void TrimOldFiles()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-7);
                string[] files = Directory.GetFiles(DirectoryPath, "app-*.log");
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
