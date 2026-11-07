using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ScreenCurtain
{
    internal static class AppIcons
    {
        public static Icon TrayActive
        {
            get { return Load("TrayActive"); }
        }

        public static Icon TrayDegraded
        {
            get { return Load("TrayDegraded"); }
        }

        public static Icon TrayOff
        {
            get { return Load("TrayOff"); }
        }

        public static Icon Load(string name)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("ScreenCurtain.Resources." + name + ".ico"))
                {
                    if (stream == null) return null;
                    return new Icon(stream);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("加载图标资源失败：" + name, ex);
                return null;
            }
        }
    }
}
