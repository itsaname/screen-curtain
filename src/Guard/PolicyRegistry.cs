using Microsoft.Win32;

namespace ScreenCurtain
{
    internal static class PolicyRegistry
    {
        public static RegistryHive Hive = RegistryHive.LocalMachine;
        public static RegistryView View = RegistryView.Registry64;
        public static string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
        public static string BackupKeyPath = @"SOFTWARE\ScreenCurtain\GuardBackup";
        public static IPolicyStore Store = new RegistryPolicyStore();

        public static RegistryKey OpenBaseKey()
        {
            return RegistryKey.OpenBaseKey(Hive, View);
        }
    }
}
