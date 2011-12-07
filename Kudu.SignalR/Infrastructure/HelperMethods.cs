using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Kudu.SignalR.Infrastructure
{
    public static class HelperMethods
    {
        public static string Hash(string value)
        {
            return String.Join(String.Empty, MD5.Create()
                     .ComputeHash(Encoding.Default.GetBytes(value))
                     .Select(b => b.ToString("x2")));
        }
    }
}
