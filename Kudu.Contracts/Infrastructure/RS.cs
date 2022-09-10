using System;
using System.Globalization;

namespace Kudu.Common
{
    /// <summary>
    /// Utility class for string resources.
    /// </summary>
    public static class RS
    {
        /// <summary>
        /// Formats the specified resource string using <see cref="System.Globalization.CultureInfo.CurrentCulture"/>.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <returns>The formatted string.</returns>
        public static string Format(string format, params object[] args)
        {
            return String.Format(CultureInfo.CurrentCulture, format, args);
        }
    }
}
