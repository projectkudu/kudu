using System;

namespace Kudu.Contracts.Infrastructure
{
    public static class StringUtils
    {
        public static bool IsTrueLike(string value)
        {
            return !String.IsNullOrEmpty(value) && (value == "1" || value.Equals(Boolean.TrueString, StringComparison.OrdinalIgnoreCase));
        }
    }
}
