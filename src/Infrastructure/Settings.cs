using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class HotkeySpec
    {
        public uint Modifiers;
        public uint VirtualKey;
        public string Text;

        public static HotkeySpec Default
        {
            get { return Parse("Ctrl+Alt+Shift+L"); }
        }

        public static string ModifiersToText(uint mods)
        {
            List<string> parts = new List<string>();
            if ((mods & (uint)Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((mods & (uint)Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((mods & (uint)Native.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((mods & (uint)Native.MOD_WIN) != 0) parts.Add("Win");
            return string.Join("+", parts.ToArray());
        }

        public static HotkeySpec Parse(string text)
        {            HotkeySpec fallback = new HotkeySpec();
            fallback.Modifiers = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT;
            fallback.VirtualKey = (uint)Keys.L;
            fallback.Text = "Ctrl+Alt+Shift+L";

            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return fallback;
            }

            uint mods = 0;
            Keys key = Keys.None;
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (i == parts.Length - 1)
                {
                    if (!Enum.TryParse<Keys>(token, true, out key))
                    {
                        return fallback;
                    }
                }
                else if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                         token.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= (uint)Native.MOD_CONTROL;
                }
                else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= (uint)Native.MOD_ALT;
                }
                else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= (uint)Native.MOD_SHIFT;
                }
                else if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                         token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= (uint)Native.MOD_WIN;
                }
                else
                {
                    return fallback;
                }
            }

            if (key == Keys.None)
            {
                return fallback;
            }

            HotkeySpec spec = new HotkeySpec();
            spec.Modifiers = mods;
            spec.VirtualKey = (uint)key;
            spec.Text = text.Trim();
            return spec;
        }
    }

    internal enum CloseAction
    {
        Minimize,
        Exit
    }

    internal sealed class Settings
    {
        public const string DefaultHotkey = "Ctrl+Alt+Shift+L";

        public bool GuardEnabled = true;
        public bool FirstRun = true;
        public bool AllowWheelWake = false;
        public bool PreventSleep = true;
        public bool StartMinimized = false;
        public bool StartWithWindows = false;
        public string Language = "auto";
        public string Theme = "auto";
        public string GuardStartMode = "startup";
        public bool TryPhysicalOff = true;
        public bool LaptopPhysicalOff = false;
        public bool DimExternalBacklight = true;
        public bool RestoreForeground = true;
        public CloseAction CloseAction = CloseAction.Minimize;
        public bool ConfirmExit = true;
        public bool ShowTrayNotifications = true;
        public bool MuteSystemAudio = true;
        public string Hotkey = DefaultHotkey;

        private static string DirectoryPath
        {
            get { return AppPaths.DataDirectory; }
        }

        public static string FilePath
        {
            get { return AppPaths.SettingsFile; }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return s;
                }

                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    values[key] = value;
                }

                s.GuardEnabled = ReadBool(values, "GuardEnabled", s.GuardEnabled);
                s.FirstRun = ReadBool(values, "FirstRun", s.FirstRun);
                s.AllowWheelWake = ReadBool(values, "AllowWheelWake", s.AllowWheelWake);
                s.PreventSleep = ReadBool(values, "PreventSleep", s.PreventSleep);
                s.StartMinimized = ReadBool(values, "StartMinimized", s.StartMinimized);
                s.StartWithWindows = ReadBool(values, "StartWithWindows", s.StartWithWindows);
                string language = ReadString(values, "Language", s.Language).ToLowerInvariant();
                if (language == "zh" || language == "en" || language == "auto") s.Language = language;
                string theme = ReadString(values, "Theme", s.Theme).ToLowerInvariant();
                if (theme == "light" || theme == "dark" || theme == "auto") s.Theme = theme;
                string guardMode = ReadString(values, "GuardStartMode", s.GuardStartMode).ToLowerInvariant();
                if (guardMode == "startup" || guardMode == "blackout") s.GuardStartMode = guardMode;
                s.TryPhysicalOff = ReadBool(values, "TryPhysicalOff", s.TryPhysicalOff);
                s.LaptopPhysicalOff = ReadBool(values, "LaptopPhysicalOff", s.LaptopPhysicalOff);
                s.DimExternalBacklight = ReadBool(values, "DimExternalBacklight", s.DimExternalBacklight);
                s.RestoreForeground = ReadBool(values, "RestoreForeground", s.RestoreForeground);
                s.ConfirmExit = ReadBool(values, "ConfirmExit", s.ConfirmExit);
                s.ShowTrayNotifications = ReadBool(values, "ShowTrayNotifications", s.ShowTrayNotifications);
                s.MuteSystemAudio = ReadBool(values, "MuteSystemAudio", s.MuteSystemAudio);
                s.CloseAction = ReadCloseAction(values, "CloseAction", s.CloseAction);
                s.Hotkey = ReadString(values, "Hotkey", s.Hotkey);

                HotkeySpec spec = HotkeySpec.Parse(s.Hotkey);
                if (spec == null || spec.VirtualKey == 0)
                {
                    s.Hotkey = DefaultHotkey;
                }
            }
            catch (Exception ex)
            {
                TryBackupBadSettings();
                Log.Exception("读取设置失败，使用默认设置", ex);
            }
            return s;
        }

        public void Save()
        {
            string tmp = FilePath + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(DirectoryPath);
                string[] lines = new string[]
                {
                    "# ScreenCurtain settings",
                    "SettingsVersion=2",
                    "GuardEnabled=" + (GuardEnabled ? "true" : "false"),
                    "FirstRun=" + (FirstRun ? "true" : "false"),
                    "AllowWheelWake=" + (AllowWheelWake ? "true" : "false"),
                    "PreventSleep=" + (PreventSleep ? "true" : "false"),
                    "StartMinimized=" + (StartMinimized ? "true" : "false"),
                    "StartWithWindows=" + (StartWithWindows ? "true" : "false"),
                    "Language=" + Language,
                    "Theme=" + Theme,
                    "GuardStartMode=" + GuardStartMode,
                    "TryPhysicalOff=" + (TryPhysicalOff ? "true" : "false"),
                    "LaptopPhysicalOff=" + (LaptopPhysicalOff ? "true" : "false"),
                    "DimExternalBacklight=" + (DimExternalBacklight ? "true" : "false"),
                    "RestoreForeground=" + (RestoreForeground ? "true" : "false"),
                    "CloseAction=" + (CloseAction == CloseAction.Exit ? "Exit" : "Minimize"),
                    "ConfirmExit=" + (ConfirmExit ? "true" : "false"),
                    "ShowTrayNotifications=" + (ShowTrayNotifications ? "true" : "false"),
                    "MuteSystemAudio=" + (MuteSystemAudio ? "true" : "false"),
                    "Hotkey=" + Hotkey
                };
                File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(tmp, FilePath, null);
                    }
                    catch
                    {
                        File.Delete(FilePath);
                        File.Move(tmp, FilePath);
                    }
                }
                else
                {
                    File.Move(tmp, FilePath);
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                Log.Exception("保存设置失败", ex);
            }
        }

        public HotkeySpec GetHotkey()
        {
            HotkeySpec spec = HotkeySpec.Parse(Hotkey);
            if (spec == null)
            {
                spec = HotkeySpec.Default;
            }
            return spec;
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool defaultValue)
        {
            string raw;
            if (!values.TryGetValue(key, out raw))
            {
                return defaultValue;
            }
            bool parsed;
            if (bool.TryParse(raw, out parsed))
            {
                return parsed;
            }
            if (raw == "1")
            {
                return true;
            }
            if (raw == "0")
            {
                return false;
            }
            return defaultValue;
        }

        private static CloseAction ReadCloseAction(Dictionary<string, string> values, string key, CloseAction defaultValue)
        {
            string raw;
            if (values.TryGetValue(key, out raw))
            {
                if (string.Equals(raw, "Exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "退出", StringComparison.OrdinalIgnoreCase))
                {
                    return CloseAction.Exit;
                }
                if (string.Equals(raw, "Minimize", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "最小化", StringComparison.OrdinalIgnoreCase))
                {
                    return CloseAction.Minimize;
                }
            }
            return defaultValue;
        }

        private static void TryBackupBadSettings()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                string backup = FilePath + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(FilePath, backup, true);
            }
            catch
            {
            }
        }

        private static string ReadString(Dictionary<string, string> values, string key, string defaultValue)
        {
            string raw;
            if (values.TryGetValue(key, out raw) && !string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
            return defaultValue;
        }
    }
}
