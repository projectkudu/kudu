using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;

namespace Kudu.Services
{
    public static class InstanceIdUtility
    {
        private static string _instanceId;

        public static string GetInstanceId(HttpContextBase context)
        {
            EnsureInstanceId(context);
            return _instanceId;
        }

        public static string GetShortInstanceId(HttpContextBase context)
        {
            EnsureInstanceId(context);
            Debug.Assert(_instanceId.Length >= 6);

            return _instanceId.Substring(0, 6);
        }

        private static void EnsureInstanceId(HttpContextBase context)
        {
            if (_instanceId != null)
            {
                return;
            }

            string value = GetInstanceIdInternal(context, Environment.MachineName);
            Interlocked.Exchange(ref _instanceId, value);
        }

        internal static string GetInstanceIdInternal(HttpContextBase context, string machineName)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            string identifier = context.Request.ServerVariables["LOCAL_ADDR"];
            if (String.IsNullOrEmpty(identifier))
            {
                identifier = machineName;
            }

            using (var sha1 = new SHA256CryptoServiceProvider())
            {
                byte[] tokenBytes = Encoding.Unicode.GetBytes(identifier);
                byte[] hashBytes = sha1.ComputeHash(tokenBytes);

                return ConvertToString(hashBytes);
            }
        }

        private static char HexToASCII(int c)
        {
            return (char)((c < 10) ? (c + '0') : (c + 'a' - 10));
        }

        private static string ConvertToString(byte[] input)
        {
            char[] buffer = new char[2 * 32]; // 64 BYTES
            for (int i = 0; i < input.Length; i++)
            {
                buffer[2 * i] = HexToASCII((input[i] & 0xF0) >> 4);
                buffer[2 * i + 1] = HexToASCII(input[i] & 0xF);
            }

            return new string(buffer);
        }

    }
}
