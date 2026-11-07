using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace ScreenCurtain
{
    internal static class PipeProtocol
    {
        public const string Hello = "HELLO";
        public const string Ok = "OK";
        public const string Deny = "DENY";
        public const string Active = "ACTIVE";
        public const string Restore = "RESTORE";
        public const string Restored = "RESTORED";
        public const string Pause = "PAUSE";
        public const string Paused = "PAUSED";
        public const string Resume = "RESUME";
        public const string Error = "ERROR";
        public const string Degraded = "DEGRADED";

        public static StreamReader NewReader(Stream stream)
        {
            return new StreamReader(stream, new UTF8Encoding(false), false, 4096, true);
        }

        public static StreamWriter NewWriter(Stream stream)
        {
            StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true);
            writer.NewLine = "\n";
            writer.AutoFlush = true;
            return writer;
        }

        public static NamedPipeServerStream CreateServer(string pipeName)
        {
            try
            {
                PipeSecurity security = new PipeSecurity();
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                if (identity.User != null)
                {
                    security.AddAccessRule(new PipeAccessRule(identity.User, PipeAccessRights.FullControl, AccessControlType.Allow));
                }
                SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));
                return new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    security);
            }
            catch (Exception ex)
            {
                Log.Exception("创建带 ACL 的命名管道失败，回退默认 ACL", ex);
                return new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096);
            }
        }

        public static string Argument(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        public static int IntArgument(string[] args, string name, int fallback)
        {
            string text = Argument(args, name);
            int value;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            return fallback;
        }

        public static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
