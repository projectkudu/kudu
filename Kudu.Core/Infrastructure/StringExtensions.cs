using System;
using System.Globalization;

namespace Kudu.Core
{
    public static class StringExtensions
    {
        public static string FormatInvariant(this string format, params object[] args)
        {
            return String.Format(CultureInfo.InvariantCulture, format, args);
        }

        public static string FormatCurrentCulture(this string format, params object[] args)
        {
            return String.Format(CultureInfo.CurrentCulture, format, args);
        }

        /// <summary>
        /// Make string PII safe.
        /// Use this method for sensitive personal information to hides its content.
        /// </summary>
        public static string Fuzz(this string str)
        {
            return String.IsNullOrEmpty(str) ? str : str.GetHashCode().ToString();
        }

        public static string EscapeHashCharacter(this string str)
        {
            return str.Replace("#", Uri.EscapeDataString("#"));
        }
    }
}
