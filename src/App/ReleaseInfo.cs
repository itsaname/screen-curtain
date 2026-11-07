using System;
using System.Reflection;

namespace ScreenCurtain
{
    internal static class ReleaseInfo
    {
        public static string Version
        {
            get
            {
                try
                {
                    Version v = Assembly.GetExecutingAssembly().GetName().Version;
                    return v.Major + "." + v.Minor + "." + v.Build;
                }
                catch
                {
                    return "1.3.0";
                }
            }
        }

        public static string FullVersion
        {
            get
            {
                try { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
                catch { return "1.3.0.0"; }
            }
        }
    }
}

