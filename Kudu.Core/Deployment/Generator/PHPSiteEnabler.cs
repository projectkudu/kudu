using System.IO;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class PHPSiteEnabler
    {
        private static readonly string[] IisStartupFiles = new[]
        {
            "default.htm", "default.html", "default.asp", "index.htm", "index.html", "iisstart.htm", "default.aspx"
        };

        private static readonly string[] PHPDetectionFiles = new[] { "composer.json" };

        private static readonly string[] PotentialPHPDetectionFiles = new[] { "composer.lock" };

        public static bool LooksLikePHP(string siteFolder)
        {
            // only support linux
            if (OSDetector.IsOnWindows())
            {
                return false;
            }

            bool potentiallyLooksLikePHP = false;

            // If any of the files in PHPDetectionFiles exist
            // We assume it's PHP
            foreach (var PHPDetectionFile in PHPDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, PHPDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    return true;
                }
            }

            // If any of the files in PotentialPHPDetectionFiles exist
            // We assume it can potentially be PHP
            foreach (var PHPDetectionFile in PotentialPHPDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, PHPDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    potentiallyLooksLikePHP = true;
                    break;
                }
            }

            // If we assume it is potentially a PHP site
            if (potentiallyLooksLikePHP)
            {
                // Check if any of the known iis start pages (excluding index.php) exist
                // If so, then it is not a PHP web site otherwise it is
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
