using System;
using Microsoft.Win32;

namespace ScreenCurtain
{
    internal interface IPolicyStore
    {
        bool KeyExists(string path);
        bool ValueExists(string path, string name);
        RegistryValueKind GetValueKind(string path, string name);
        object GetValue(string path, string name);
        void SetValue(string path, string name, object value, RegistryValueKind kind);
        void DeleteValue(string path, string name);
        int ValueCount(string path);
        int SubKeyCount(string path);
        void DeleteKeyTree(string path);
    }

    internal sealed class RegistryPolicyStore : IPolicyStore
    {
        public bool KeyExists(string path)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, false))
            {
                return key != null;
            }
        }

        public bool ValueExists(string path, string name)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, false))
            {
                if (key == null) return false;
                string[] names = key.GetValueNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
        }

        public RegistryValueKind GetValueKind(string path, string name)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, false))
            {
                if (key == null) return RegistryValueKind.Unknown;
                return key.GetValueKind(name);
            }
        }

        public object GetValue(string path, string name)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, false))
            {
                if (key == null) return null;
                return key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
        }

        public void SetValue(string path, string name, object value, RegistryValueKind kind)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.CreateSubKey(path))
            {
                key.SetValue(name, value, kind);
            }
        }

        public void DeleteValue(string path, string name)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, true))
            {
                if (key != null) key.DeleteValue(name, false);
            }
        }

        public int ValueCount(string path)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, false))
            {
                return key == null ? 0 : key.ValueCount;
            }
        }

        public int SubKeyCount(string path)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            using (RegistryKey key = baseKey.OpenSubKey(path, false))
            {
                return key == null ? 0 : key.SubKeyCount;
            }
        }

        public void DeleteKeyTree(string path)
        {
            using (RegistryKey baseKey = PolicyRegistry.OpenBaseKey())
            {
                baseKey.DeleteSubKeyTree(path, false);
            }
        }
    }
}
