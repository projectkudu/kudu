using System.IO;
using System.IO.Abstractions;

namespace Kudu.Core.Deployment.Generator
{
    public class NodeSiteEnabler
    {
        private static readonly string[] NodeStartFiles = new[] { "server.js", "app.js" };

        public static bool LooksLikeNode(IFileSystem fileSystem, string siteFolder)
        {
            // Check if any of the known start pages exist
            foreach (var nodeDetectionFile in NodeStartFiles)
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
