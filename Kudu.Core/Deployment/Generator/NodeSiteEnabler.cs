using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class NodeSiteEnabler
    {
        private static readonly string[] NodeDetectionFiles = new[] { "server.js", "app.js", "package.json" };

        public static bool LooksLikeNode(IFileSystem fileSystem, string siteFolder)
        {
            // Check if any of the known start pages exist
            foreach (var nodeDetectionFile in NodeDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, nodeDetectionFile);
                if (fileSystem.File.Exists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
