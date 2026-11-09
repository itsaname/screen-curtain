using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace ScreenCurtain
{
    internal sealed class MemoryPolicyStore : IPolicyStore
    {
        private sealed class Entry
        {
            public object Value;
            public RegistryValueKind Kind;
        }

        private readonly Dictionary<string, Dictionary<string, Entry>> _keys =
            new Dictionary<string, Dictionary<string, Entry>>(StringComparer.OrdinalIgnoreCase);

        private static string Norm(string path)
        {
            return (path ?? string.Empty).Trim('\\');
        }

        public bool KeyExists(string path)
        {
            return _keys.ContainsKey(Norm(path));
        }

        public bool ValueExists(string path, string name)
        {
            Dictionary<string, Entry> values;
            return _keys.TryGetValue(Norm(path), out values) && values.ContainsKey(name);
        }

        public RegistryValueKind GetValueKind(string path, string name)
        {
            Dictionary<string, Entry> values;
            Entry entry;
            if (_keys.TryGetValue(Norm(path), out values) && values.TryGetValue(name, out entry))
            {
                return entry.Kind;
            }
            return RegistryValueKind.Unknown;
        }

        public object GetValue(string path, string name)
        {
            Dictionary<string, Entry> values;
            Entry entry;
            if (_keys.TryGetValue(Norm(path), out values) && values.TryGetValue(name, out entry))
            {
                return entry.Value;
            }
            return null;
        }

        public void SetValue(string path, string name, object value, RegistryValueKind kind)
        {
            string key = Norm(path);
            Dictionary<string, Entry> values;
            if (!_keys.TryGetValue(key, out values))
            {
                values = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                _keys[key] = values;
            }
            Entry entry = new Entry();
            entry.Value = value;
            entry.Kind = kind;
            values[name] = entry;
        }

        public void DeleteValue(string path, string name)
        {
            Dictionary<string, Entry> values;
            if (_keys.TryGetValue(Norm(path), out values))
            {
                values.Remove(name);
            }
        }

        public int ValueCount(string path)
        {
            Dictionary<string, Entry> values;
            if (_keys.TryGetValue(Norm(path), out values))
            {
                return values.Count;
            }
            return 0;
        }

        public int SubKeyCount(string path)
        {
            string prefix = Norm(path) + "\\";
            int count = 0;
            foreach (string key in _keys.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) count++;
            }
            return count;
        }

        public void DeleteKeyTree(string path)
        {
            string key = Norm(path);
            string prefix = key + "\\";
            List<string> remove = new List<string>();
            foreach (string existing in _keys.Keys)
            {
                if (string.Equals(existing, key, StringComparison.OrdinalIgnoreCase) ||
                    existing.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    remove.Add(existing);
                }
            }
            for (int i = 0; i < remove.Count; i++)
            {
                _keys.Remove(remove[i]);
            }
        }
    }

    internal static class PolicyTest
    {
        public static void Run()
        {
            StringBuilder result = new StringBuilder();
            bool passed = true;
            string trace = Environment.GetEnvironmentVariable("SCREENCURTAIN_RESULT_FILE");
            if (string.IsNullOrWhiteSpace(trace)) trace = Path.Combine(Path.GetTempPath(), "ScreenCurtain-policy-test.txt");
            trace += ".trace";
            try
            {
                File.WriteAllText(trace, "start\n");
                result.AppendLine("ScreenCurtain policy snapshot/restore test (in-memory registry)");
                result.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                File.AppendAllText(trace, "test1\n");
                passed &= TestNoExistingKey(result);
                File.AppendAllText(trace, "test2\n");
                passed &= TestExistingKey(result);
                File.AppendAllText(trace, "test3\n");
                passed &= TestExternalModification(result);
                File.AppendAllText(trace, "test4\n");
                passed &= TestOrphanRecovery(result);
                File.AppendAllText(trace, "test5\n");
                passed &= TestPauseResumeCrash(result);
                File.AppendAllText(trace, "test6\n");
                passed &= TestHealthCheck(result);
                File.AppendAllText(trace, "done\n");

                result.AppendLine("RESULT: " + (passed ? "PASS" : "FAIL"));
            }
            catch (Exception ex)
            {
                passed = false;
                result.AppendLine("EXCEPTION: " + ex);
                result.AppendLine("RESULT: FAIL");
                Log.Exception("策略测试异常", ex);
            }

            string path = Environment.GetEnvironmentVariable("SCREENCURTAIN_RESULT_FILE");
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-policy-test.txt");
            }
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, result.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static bool TestNoExistingKey(StringBuilder result)
        {
            bool ok = true;
            ResetStore();
            result.AppendLine();
            result.AppendLine("[1] No existing AU key");
            PolicySnapshot snapshot = PolicyManager.Capture(1001);
            ok &= Check(!snapshot.KeyExisted, "capture: key did not exist", result);
            snapshot.Save();
            PolicyManager.ApplyTemporary();
            string error;
            ok &= Check(PolicyManager.VerifyTemporary(out error), "temporary values applied", result);
            PolicyManager.Restore(snapshot);
            ok &= Check(!PolicyRegistry.Store.KeyExists(PolicyRegistry.PolicyKeyPath), "AU key removed after restore", result);
            ok &= Check(!PolicySnapshot.HasBackup(), "backup removed after restore", result);
            return ok;
        }

        private static bool TestExistingKey(StringBuilder result)
        {
            bool ok = true;
            ResetStore();
            result.AppendLine();
            result.AppendLine("[2] Existing AU key with custom value");
            IPolicyStore store = PolicyRegistry.Store;
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.PolicyKeyPath, "AlwaysAutoRebootAtScheduledTime", 1, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.PolicyKeyPath, "KeepMe", 42, RegistryValueKind.DWord);

            PolicySnapshot snapshot = PolicyManager.Capture(1002);
            ok &= Check(snapshot.KeyExisted, "capture: key existed", result);
            snapshot.Save();
            PolicyManager.ApplyTemporary();
            string error;
            ok &= Check(PolicyManager.VerifyTemporary(out error), "temporary values applied", result);
            PolicyManager.Restore(snapshot);
            ok &= Check(store.KeyExists(PolicyRegistry.PolicyKeyPath), "AU key preserved", result);
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 0, "NoAutoUpdate restored to 0", result);
            ok &= Check(!store.ValueExists(PolicyRegistry.PolicyKeyPath, "NoAutoRebootWithLoggedOnUsers"), "missing NoAutoReboot stays missing", result);
            ok &= Check(ReadInt(store, "AlwaysAutoRebootAtScheduledTime", -1) == 1, "AlwaysAutoReboot restored to 1", result);
            ok &= Check(ReadInt(store, "KeepMe", -1) == 42, "custom value preserved", result);
            ok &= Check(!PolicySnapshot.HasBackup(), "backup removed after restore", result);
            return ok;
        }

        private static bool TestExternalModification(StringBuilder result)
        {
            bool ok = true;
            ResetStore();
            result.AppendLine();
            result.AppendLine("[3] External modification is not overwritten");
            IPolicyStore store = PolicyRegistry.Store;
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 1, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoRebootWithLoggedOnUsers", 1, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.PolicyKeyPath, "AlwaysAutoRebootAtScheduledTime", 1, RegistryValueKind.DWord);

            PolicySnapshot snapshot = PolicyManager.Capture(1003);
            snapshot.Save();
            PolicyManager.ApplyTemporary();
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
            PolicyManager.Restore(snapshot);
            ok &= Check(store.KeyExists(PolicyRegistry.PolicyKeyPath), "AU key exists", result);
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 0, "external NoAutoUpdate=0 preserved", result);
            ok &= Check(ReadInt(store, "NoAutoRebootWithLoggedOnUsers", -1) == 1, "other value restored", result);
            ok &= Check(ReadInt(store, "AlwaysAutoRebootAtScheduledTime", -1) == 1, "other value restored", result);
            ok &= Check(!PolicySnapshot.HasBackup(), "backup removed after restore", result);
            return ok;
        }

        private static bool TestOrphanRecovery(StringBuilder result)
        {
            bool ok = true;
            ResetStore();
            result.AppendLine();
            result.AppendLine("[4] Orphan backup recovery");
            IPolicyStore store = PolicyRegistry.Store;
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.PolicyKeyPath, "AlwaysAutoRebootAtScheduledTime", 1, RegistryValueKind.DWord);

            PolicySnapshot snapshot = PolicyManager.Capture(1004);
            snapshot.Save();
            PolicyManager.ApplyTemporary();
            // Simulate a crash: do not call Restore; next start recovers the backup.
            PolicyManager.RecoverOrphaned();
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 0, "orphan restore: NoAutoUpdate back to 0", result);
            ok &= Check(!store.ValueExists(PolicyRegistry.PolicyKeyPath, "NoAutoRebootWithLoggedOnUsers"), "orphan restore: absent value removed", result);
            ok &= Check(ReadInt(store, "AlwaysAutoRebootAtScheduledTime", -1) == 1, "orphan restore: Always back to 1", result);
            ok &= Check(!PolicySnapshot.HasBackup(), "orphan backup cleared", result);
            return ok;
        }

        private static bool TestPauseResumeCrash(StringBuilder result)
        {
            bool ok = true;
            ResetStore();
            result.AppendLine();
            result.AppendLine("[5] Pause/resume preserves backup, crash recovers originals");
            IPolicyStore store = PolicyRegistry.Store;
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
            store.SetValue(PolicyRegistry.PolicyKeyPath, "AlwaysAutoRebootAtScheduledTime", 1, RegistryValueKind.DWord);

            PolicySnapshot snapshot = PolicyManager.Capture(1005);
            snapshot.Save();
            PolicyManager.ApplyTemporary();

            // PAUSE restores the user's values but must keep the backup for a
            // later crash during RESUME.
            PolicyManager.Restore(snapshot, false);
            ok &= Check(PolicySnapshot.HasBackup(), "pause keeps backup", result);
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 0, "pause restored NoAutoUpdate", result);

            // RESUME re-applies temporary values; simulate a crash now.
            PolicyManager.ApplyTemporary();
            PolicyManager.RecoverOrphaned();
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 0, "crash after resume restores NoAutoUpdate", result);
            ok &= Check(ReadInt(store, "AlwaysAutoRebootAtScheduledTime", -1) == 1, "crash after resume restores Always", result);
            ok &= Check(!PolicySnapshot.HasBackup(), "orphan recovery clears backup", result);
            return ok;
        }

        private static bool TestHealthCheck(StringBuilder result)
        {            bool ok = true;
            ResetStore();
            result.AppendLine();
            result.AppendLine("[5] Health check reapplies temporary policy");
            IPolicyStore store = PolicyRegistry.Store;
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
            PolicySnapshot snapshot = PolicyManager.Capture(1005);
            snapshot.Save();
            PolicyManager.ApplyTemporary();
            store.SetValue(PolicyRegistry.PolicyKeyPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);

            string error;
            bool healthy = PolicyManager.CheckHealth(out error);
            ok &= Check(healthy, "health check returns healthy after reapply", result);
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 1, "NoAutoUpdate reapplied to 1", result);
            PolicyManager.Restore(snapshot);
            ok &= Check(ReadInt(store, "NoAutoUpdate", -1) == 0, "restore after health check returns original 0", result);
            return ok;
        }

        private static void ResetStore()
        {
            PolicyRegistry.Store = new MemoryPolicyStore();
            PolicyRegistry.PolicyKeyPath = @"TestPolicy\AU";
            PolicyRegistry.BackupKeyPath = @"TestBackup";
        }

        private static int ReadInt(IPolicyStore store, string name, int fallback)
        {
            object raw = store.GetValue(PolicyRegistry.PolicyKeyPath, name);
            if (raw == null) return fallback;
            try { return Convert.ToInt32(raw, CultureInfo.InvariantCulture); } catch { return fallback; }
        }

        private static bool Check(bool condition, string description, StringBuilder result)
        {
            result.AppendLine((condition ? "  PASS: " : "  FAIL: ") + description);
            return condition;
        }
    }
}
