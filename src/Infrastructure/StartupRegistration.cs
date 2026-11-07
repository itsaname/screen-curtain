using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenCurtain
{
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ScreenCurtain";

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        key.SetValue(ValueName, CommandLine(), RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("设置开机自启失败", ex);
                return false;
            }
        }

        private static string CommandLine()
        {
            return "\"" + Application.ExecutablePath + "\"";
        }
    }
}
