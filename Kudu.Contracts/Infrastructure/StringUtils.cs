﻿using System;

namespace Kudu.Contracts.Infrastructure
{
    public static class StringUtils
    {
        public static bool IsTrueLike(string value)
        {
            return !String.IsNullOrEmpty(value) && (value == "1" || value.Equals(Boolean.TrueString, StringComparison.OrdinalIgnoreCase));
        }

        // Careful... returns "true" if the string value should be considered a boolean "false".
        public static bool IsFalseLike(string value)
        {
            return !String.IsNullOrEmpty(value) && (value == "0" || value.Equals(Boolean.FalseString, StringComparison.OrdinalIgnoreCase));
        }

        // Like bool.TryParse but accepts "0" and "1" as well.
        // This is for when there is no hard default and we need to see if the configured
        // value is a confirmed "true" or "false" in order to override behavior
        public static bool TryParseBoolean(string value, out bool result)
        {
            if (IsTrueLike(value))
            {
                result = true;
                return true;
            }
            else if (IsFalseLike(value))
            {
                result = false;
                return true;
            }
            else
            {
                result = false;
                return false;
            }
        }

        public static string ObfuscatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var index = path.IndexOf('?');
            if (index < 0)
            {
                return path;
            }

            return $"{path.Substring(0, index + 1)}...";
        }

        public static string ObfuscateUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return $"{uri.Scheme}://{uri.Authority}{StringUtils.ObfuscatePath(uri.PathAndQuery)}";
            }

            return StringUtils.ObfuscatePath(url);
        }

        public static string ObfuscateUserName(this string value)
        {
            if (string.IsNullOrEmpty(value) || string.Equals("N/A", value, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return $"##{value.Length}##";
        }
    }
}
