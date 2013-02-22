using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kudu.Web.Infrastructure
{
    public static class SlugGenerator
    {
        public static string GenerateSlug(this string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return value;
            }

            string str = RemoveAccent(value).ToLower(CultureInfo.InvariantCulture);

            str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // invalid chars           
            str = Regex.Replace(str, @"\s+", " ").Trim(); // convert multiple spaces into one space   
            str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim(); // cut and trim it   
            str = Regex.Replace(str, @"\s", "-"); // hyphens   

            return str;
        }

        private static string RemoveAccent(string value)
        {
            byte[] bytes = Encoding.GetEncoding("Cyrillic").GetBytes(value);
            return Encoding.ASCII.GetString(bytes);
        }
    }
}