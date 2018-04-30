using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class RubySiteEnabler
    {
        private static readonly string[] IisStartupFiles = new[]
        {
            "default.htm", "default.html", "default.asp", "index.htm", "index.html", "iisstart.htm", "default.aspx", "index.php"
        };

        private static readonly string[] RailsDetectionFiles = new[] { "Gemfile" };

        private static readonly string[] PotentialRailsDetectionFiles = new[] { "Gemfile.lock", "config.ru" };

        public static bool LooksLikeRuby(string siteFolder)
        {
            // only support linux
            if (OSDetector.IsOnWindows())
            {
                return false;
            }

            bool potentiallyLooksLikeRails = false;

            // If any of the files in RailsDetectionFiles exist
            // We assume it's rails
            foreach (var railsDetectionFile in RailsDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, railsDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    return true;
                }
            }

            // If any of the files in PotentialRailsDetectionFiles exist
            // We assume it can potentially be rails
            foreach (var railsDetectionFile in PotentialRailsDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, railsDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    potentiallyLooksLikeRails = true;
                    break;
                }
            }

            // If we assume it is potentially a rails site
            if (potentiallyLooksLikeRails)
            {
                // Check if any of the known iis start pages exist
                // If so, then it is not a rails web site otherwise it is
                foreach (var iisStartupFile in IisStartupFiles)
                {
                    string fullPath = Path.Combine(siteFolder, iisStartupFile);
                    if (FileSystemHelpers.FileExists(fullPath))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
