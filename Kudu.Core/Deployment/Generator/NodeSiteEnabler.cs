using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class NodeSiteEnabler
    {
        private static readonly string[] IisStartupFiles = new[]
        {
            "default.htm", "default.html", "default.asp", "index.htm", "index.html", "iisstart.htm", "default.aspx", "index.php"
        };

        private static readonly string[] NodeDetectionFiles = new[] { "package.json" };

        private static readonly string[] PotentialNodeDetectionFiles = new[] { "server.js", "app.js" };

        public static bool LooksLikeNode(string siteFolder)
        {
            bool potentiallyLooksLikeNode = false;

            // If any of the files in NodeDetectionFiles exist
            // We assume it's node.js
            foreach (var nodeDetectionFile in NodeDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, nodeDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    return true;
                }
            }

            // If any of the files in PotentialNodeDetectionFiles exist
            // We assume it can potentially be node.js
            foreach (var nodeDetectionFile in PotentialNodeDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, nodeDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    potentiallyLooksLikeNode = true;
                    break;
                }
            }

            // If we assume it is potentially a node.js site
            if (potentiallyLooksLikeNode)
            {
                // Check if any of the known iis start pages exist
                // If so, then it is not a node.js web site otherwise it is
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
