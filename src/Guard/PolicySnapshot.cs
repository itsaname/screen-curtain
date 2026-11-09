using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;

namespace ScreenCurtain
{
    internal sealed class PolicyValueSnapshot
    {
        public bool Existed;
        public RegistryValueKind Kind;
        public object Value;
    }

    internal sealed class PolicySnapshot
    {
        public static readonly string[] PolicyNames = new string[]
        {
            "NoAutoUpdate",
            "NoAutoRebootWithLoggedOnUsers",
            "AlwaysAutoRebootAtScheduledTime"
        };

        public bool KeyExisted;
        public int ParentPid;
        public int SessionId;
        public string CreatedAtUtc;
        public string AppVersion;
        public Dictionary<string, PolicyValueSnapshot> Values = new Dictionary<string, PolicyValueSnapshot>(StringComparer.OrdinalIgnoreCase);

        public static PolicySnapshot Capture(int parentPid)
        {
            IPolicyStore store = PolicyRegistry.Store;
            PolicySnapshot snapshot = new PolicySnapshot();
            snapshot.ParentPid = parentPid;
            try { snapshot.SessionId = Process.GetCurrentProcess().SessionId; } catch { snapshot.SessionId = 0; }
            snapshot.CreatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            snapshot.AppVersion = "1.0.0.0";
            snapshot.KeyExisted = store.KeyExists(PolicyRegistry.PolicyKeyPath);

            for (int i = 0; i < PolicyNames.Length; i++)
            {
                string name = PolicyNames[i];
                PolicyValueSnapshot value = new PolicyValueSnapshot();
                if (snapshot.KeyExisted && store.ValueExists(PolicyRegistry.PolicyKeyPath, name))
                {
                    value.Existed = true;
                    value.Kind = store.GetValueKind(PolicyRegistry.PolicyKeyPath, name);
                    value.Value = store.GetValue(PolicyRegistry.PolicyKeyPath, name);
                }
                snapshot.Values[name] = value;
            }

            return snapshot;
        }

        public void Save()
        {
            IPolicyStore store = PolicyRegistry.Store;
            try
            {
                if (store.KeyExists(PolicyRegistry.BackupKeyPath))
                {
                    store.DeleteKeyTree(PolicyRegistry.BackupKeyPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("清理旧策略快照失败：{0}", ex.Message);
            }

            store.SetValue(PolicyRegistry.BackupKeyPath, "KeyExisted", KeyExisted ? 1 : 0, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.BackupKeyPath, "ParentPid", ParentPid, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.BackupKeyPath, "SessionId", SessionId, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.BackupKeyPath, "CreatedAtUtc", CreatedAtUtc ?? string.Empty, RegistryValueKind.String);
            store.SetValue(PolicyRegistry.BackupKeyPath, "AppVersion", AppVersion ?? string.Empty, RegistryValueKind.String);

            for (int i = 0; i < PolicyNames.Length; i++)
            {
                string name = PolicyNames[i];
                PolicyValueSnapshot value = Values[name];
                store.SetValue(PolicyRegistry.BackupKeyPath, name + "_Existed", value.Existed ? 1 : 0, RegistryValueKind.DWord);
                store.SetValue(PolicyRegistry.BackupKeyPath, name + "_Kind", (int)value.Kind, RegistryValueKind.DWord);
                store.SetValue(PolicyRegistry.BackupKeyPath, name + "_Data", ValueToStorageString(value), RegistryValueKind.String);
            }
            Log.Info("已保存 Windows Update 策略快照");
        }

        public static PolicySnapshot Load()
        {
            IPolicyStore store = PolicyRegistry.Store;
            if (!store.KeyExists(PolicyRegistry.BackupKeyPath))
            {
                return null;
            }

            PolicySnapshot snapshot = new PolicySnapshot();
            snapshot.KeyExisted = ReadInt(store, PolicyRegistry.BackupKeyPath, "KeyExisted", 0) != 0;
            snapshot.ParentPid = ReadInt(store, PolicyRegistry.BackupKeyPath, "ParentPid", 0);
            snapshot.SessionId = ReadInt(store, PolicyRegistry.BackupKeyPath, "SessionId", 0);
            snapshot.CreatedAtUtc = ReadString(store, PolicyRegistry.BackupKeyPath, "CreatedAtUtc");
            snapshot.AppVersion = ReadString(store, PolicyRegistry.BackupKeyPath, "AppVersion");

            for (int i = 0; i < PolicyNames.Length; i++)
            {
                string name = PolicyNames[i];
                PolicyValueSnapshot value = new PolicyValueSnapshot();
                value.Existed = ReadInt(store, PolicyRegistry.BackupKeyPath, name + "_Existed", 0) != 0;
                value.Kind = (RegistryValueKind)ReadInt(store, PolicyRegistry.BackupKeyPath, name + "_Kind", (int)RegistryValueKind.DWord);
                value.Value = ValueFromStorageString(value.Kind, ReadString(store, PolicyRegistry.BackupKeyPath, name + "_Data"));
                snapshot.Values[name] = value;
            }
            return snapshot;
        }

        public static void Clear()
        {
            try
            {
                IPolicyStore store = PolicyRegistry.Store;
                if (store.KeyExists(PolicyRegistry.BackupKeyPath))
                {
                    store.DeleteKeyTree(PolicyRegistry.BackupKeyPath);
                }
                Log.Info("已删除 Windows Update 策略快照");
            }
            catch (Exception ex)
            {
                Log.Exception("删除策略快照失败", ex);
            }
        }

        public static bool HasBackup()
        {
            return PolicyRegistry.Store.KeyExists(PolicyRegistry.BackupKeyPath);
        }

        private static string ValueToStorageString(PolicyValueSnapshot value)
        {
            if (!value.Existed || value.Value == null)
            {
                return string.Empty;
            }

            if (value.Kind == RegistryValueKind.Binary)
            {
                byte[] bytes = value.Value as byte[];
                if (bytes == null)
                {
                    return string.Empty;
                }
                return Convert.ToBase64String(bytes);
            }

            return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
        }

        private static object ValueFromStorageString(RegistryValueKind kind, string text)
        {
            if (text == null)
            {
                return null;
            }

            try
            {
                switch (kind)
                {
                    case RegistryValueKind.DWord:
                        return int.Parse(text, CultureInfo.InvariantCulture);
                    case RegistryValueKind.QWord:
                        return long.Parse(text, CultureInfo.InvariantCulture);
                    case RegistryValueKind.Binary:
                        return string.IsNullOrEmpty(text) ? new byte[0] : Convert.FromBase64String(text);
                    case RegistryValueKind.None:
                        return null;
                    default:
                        return text;
                }
            }
            catch
            {
                return text;
            }
        }

        private static int ReadInt(IPolicyStore store, string path, string name, int fallback)
        {
            object value = store.GetValue(path, name);
            if (value == null)
            {
                return fallback;
            }
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadString(IPolicyStore store, string path, string name)
        {
            object value = store.GetValue(path, name);
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
