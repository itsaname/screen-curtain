using System;
using System.Globalization;
using Microsoft.Win32;

namespace ScreenCurtain
{
    internal static class PolicyManager
    {
        public static string PolicyKeyPath
        {
            get { return PolicyRegistry.PolicyKeyPath; }
        }

        private static readonly int[] TemporaryValues = new int[] { 1, 1, 0 };

        public static PolicySnapshot Capture(int parentPid)
        {
            return PolicySnapshot.Capture(parentPid);
        }

        public static void ApplyTemporary()
        {
            IPolicyStore store = PolicyRegistry.Store;
            for (int i = 0; i < PolicySnapshot.PolicyNames.Length; i++)
            {
                store.SetValue(PolicyKeyPath, PolicySnapshot.PolicyNames[i], TemporaryValues[i], RegistryValueKind.DWord);
            }

            string error;
            if (!VerifyTemporary(out error))
            {
                throw new InvalidOperationException(error);
            }
            Log.Info("已写入临时 Windows Update 策略");
        }

        public static bool VerifyTemporary(out string error)
        {
            IPolicyStore store = PolicyRegistry.Store;
            if (!store.KeyExists(PolicyKeyPath))
            {
                error = "Windows Update AU 策略键不存在";
                return false;
            }

            for (int i = 0; i < PolicySnapshot.PolicyNames.Length; i++)
            {
                string name = PolicySnapshot.PolicyNames[i];
                if (!store.ValueExists(PolicyKeyPath, name))
                {
                    error = "策略值不存在: " + name;
                    return false;
                }

                object raw = store.GetValue(PolicyKeyPath, name);
                int value;
                try
                {
                    value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                    error = "策略值不是整数: " + name;
                    return false;
                }
                if (value != TemporaryValues[i])
                {
                    error = "策略值不匹配: " + name + "=" + value + "，期望 " + TemporaryValues[i];
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static bool CheckHealth(out string error)
        {
            if (VerifyTemporary(out error))
            {
                return true;
            }

            try
            {
                ApplyTemporary();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void Restore(PolicySnapshot snapshot, bool clearBackup = true)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            IPolicyStore store = PolicyRegistry.Store;
            bool allRestored = true;
            string skipMessage = null;

            try
            {
                if (!store.KeyExists(PolicyKeyPath))
                {
                    Log.Warn("Windows Update AU 策略键已不存在，跳过回滚并保留外部状态");
                }
                else
                {
                    for (int i = 0; i < PolicySnapshot.PolicyNames.Length; i++)
                    {
                        string name = PolicySnapshot.PolicyNames[i];
                        PolicyValueSnapshot original = snapshot.Values[name];
                        object current = store.GetValue(PolicyKeyPath, name);
                        bool isTemporary = IsTemporaryValue(current, TemporaryValues[i]);
                        if (!isTemporary)
                        {
                            if (original.Existed)
                            {
                                skipMessage = "检测到外部修改，未覆盖策略值: " + name;
                                Log.Warn(skipMessage);
                            }
                            continue;
                        }

                        if (original.Existed)
                        {
                            store.SetValue(PolicyKeyPath, name, original.Value, original.Kind);
                        }
                        else
                        {
                            store.DeleteValue(PolicyKeyPath, name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                allRestored = false;
                Log.Exception("恢复 Windows Update 策略失败", ex);
            }

            if (allRestored && !snapshot.KeyExisted)
            {
                try
                {
                    if (store.KeyExists(PolicyKeyPath) &&
                        store.ValueCount(PolicyKeyPath) == 0 &&
                        store.SubKeyCount(PolicyKeyPath) == 0)
                    {
                        store.DeleteKeyTree(PolicyKeyPath);
                        Log.Info("删除由幕帘创建的空 AU 策略键");
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("删除空 AU 策略键失败", ex);
                }
            }

            if (!allRestored)
            {
                throw new InvalidOperationException("部分 Windows Update 策略未能恢复；备份已保留以便下次重试");
            }

            if (skipMessage != null)
            {
                Log.Warn(skipMessage);
            }
            if (clearBackup)
            {
                PolicySnapshot.Clear();
            }
            Log.Info("Windows Update 策略恢复完成");
        }

        public static bool RecoverOrphaned(int currentParentPid = 0)
        {
            PolicySnapshot snapshot = PolicySnapshot.Load();
            if (snapshot == null)
            {
                return true;
            }

            if (currentParentPid > 0 && snapshot.ParentPid > 0 &&
                snapshot.ParentPid != currentParentPid && IsProcessAlive(snapshot.ParentPid))
            {
                Log.Warn("发现属于仍在运行的其它幕帘实例的策略备份（PID={0}），跳过恢复", snapshot.ParentPid);
                return false;
            }

            Log.Warn("发现上次未恢复的 Windows Update 策略备份，开始恢复");
            Restore(snapshot);
            return true;
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(pid))
                {
                    return !process.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTemporaryValue(object current, int expected)
        {
            if (current == null)
            {
                return false;
            }
            try
            {
                return Convert.ToInt32(current, CultureInfo.InvariantCulture) == expected;
            }
            catch
            {
                return false;
            }
        }
    }
}
