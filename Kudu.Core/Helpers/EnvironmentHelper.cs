using System;

namespace Kudu.Core.Helpers
{
    public static class EnvironmentHelper
    {
        public static string NormalizeBinPath(string binPath)
        {
            if (!string.IsNullOrWhiteSpace(binPath) && !OSDetector.IsOnWindows())
            {
                int binIdx = binPath.LastIndexOf("Bin", StringComparison.Ordinal);
                if (binIdx >= 0)
                {
                    string subStr = binPath.Substring(binIdx);
                    // make sure file path is end with ".....Bin" or "....Bin/"
                    if (subStr.Length < 5 && binPath.EndsWith(subStr, StringComparison.OrdinalIgnoreCase))
                    {
                        // real bin folder is lower case, but in mono, value is "Bin" instead of "bin"
                        binPath = binPath.Substring(0, binIdx) + subStr.ToLowerInvariant();
                    }
                }
            }

            return binPath;
        }
    }
}
