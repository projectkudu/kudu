using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Kudu.Core.Deployment.Generator
{
    public class NodeSiteEnabler
    {
        private static readonly string[] NonNodeExtensions = new[] { "*.php", "*.htm", "*.html", "*.aspx", "*.cshtml" };
        private const string PackageJsonFile = "package.json";

        public static bool LooksLikeNode(IFileSystem fileSystem, string siteFolder)
        {
            // If it has package.json at the root, it is node
            if (fileSystem.File.Exists(Path.Combine(siteFolder, PackageJsonFile)))
            {
                return true;
            }

            // If it has no .js files at the root, it's not Node
            if (!fileSystem.Directory.GetFiles(siteFolder, "*.js").Any())
            {
                return false;
            }

            // If it has a node_modules folder, it's likely Node
            if (fileSystem.Directory.Exists(Path.Combine(siteFolder, "node_modules")))
            {
                return true;
            }

            // If it has files that have a clear non-Node extension, treat it as non-Node
            foreach (var extension in NonNodeExtensions)
            {
                if (fileSystem.Directory.GetFiles(siteFolder, extension).Any())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
