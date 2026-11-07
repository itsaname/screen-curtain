using System;
using System.IO;

namespace ScreenCurtain
{
    internal static class AppPaths
    {
        private static readonly object Sync = new object();
        private static bool _initialized;
        private static string _baseDirectory;
        private static string _dataDirectory;
        private static string _logDirectory;
        private static bool _isPortable;
        private static bool _portableFallback;

        public static string BaseDirectory
        {
            get { EnsureInitialized(); return _baseDirectory; }
        }

        public static string DataDirectory
        {
            get { EnsureInitialized(); return _dataDirectory; }
        }

        public static string LogDirectory
        {
            get { EnsureInitialized(); return _logDirectory; }
        }

        public static bool IsPortable
        {
            get { EnsureInitialized(); return _isPortable; }
        }

        public static bool PortableFallback
        {
            get { EnsureInitialized(); return _portableFallback; }
        }

        public static string SettingsFile
        {
            get { return Path.Combine(DataDirectory, "settings.ini"); }
        }

        public static string AudioStateFile
        {
            get { return Path.Combine(DataDirectory, "audio-restore"); }
        }

        public static string DdcStateFile
        {
            get { return Path.Combine(DataDirectory, "ddc-restore"); }
        }

        public static string TopologyStateFile
        {
            get { return Path.Combine(DataDirectory, "topology-restore"); }
        }

        public static string BacklightStateFile
        {
            get { return Path.Combine(DataDirectory, "backlight-restore"); }
        }

        public static string PortableMarker
        {
            get { return Path.Combine(BaseDirectory, "portable.mode"); }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (Sync)
            {
                if (_initialized) return;
                _initialized = true;
                _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string testData = Environment.GetEnvironmentVariable("SCREENCURTAIN_DATA_DIR");
                string testLog = Environment.GetEnvironmentVariable("SCREENCURTAIN_LOG_DIR");
                string markerPath = Path.Combine(_baseDirectory, "portable.mode");
                bool marker = File.Exists(markerPath);
                _isPortable = marker || !string.IsNullOrWhiteSpace(testData);

                string localAppData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenCurtain");

                if (!string.IsNullOrWhiteSpace(testData))
                {
                    _dataDirectory = testData;
                }
                else if (marker)
                {
                    string portableData = Path.Combine(_baseDirectory, "data");
                    if (IsWritableDirectory(portableData))
                    {
                        _dataDirectory = portableData;
                    }
                    else
                    {
                        _dataDirectory = localAppData;
                        _portableFallback = true;
                    }
                }
                else
                {
                    _dataDirectory = localAppData;
                }

                if (!string.IsNullOrWhiteSpace(testLog))
                {
                    _logDirectory = testLog;
                }
                else
                {
                    _logDirectory = Path.Combine(_dataDirectory, "logs");
                }

                try
                {
                    Directory.CreateDirectory(_dataDirectory);
                    Directory.CreateDirectory(_logDirectory);
                }
                catch
                {
                    // Log/Settings will surface a later error through their own
                    // safe fallbacks; do not fail process startup here.
                }
            }
        }

        private static bool IsWritableDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                string probe = Path.Combine(path, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "test");
                File.Delete(probe);
                return true;
            }
            catch
            {
                try { Directory.Delete(path, false); } catch { }
                return false;
            }
        }
    }
}
