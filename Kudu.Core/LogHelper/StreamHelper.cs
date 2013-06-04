using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.LogHelper
{
    public class StreamHelper
    {
        public static string[] LogFileExtensions = new string[] { ".txt", ".log", ".htm" };
        /// <summary>
        /// Matches filenames to filter with .txt, .log, or .htm files
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Boolean if true or false</returns>
        public static bool MatchFilters(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                foreach (string ext in LogFileExtensions)
                {
                    if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
