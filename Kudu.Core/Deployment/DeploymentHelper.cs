using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public static class DeploymentHelper
    {
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };

        public static void CopyWithManifest(string sourcePath, string destinationPath, IDeploymentManifestReader previousManifest, bool skipOldFiles = true)
        {
            if (previousManifest != null)
            {
                var previousFiles = new HashSet<string>(previousManifest.GetPaths(), StringComparer.OrdinalIgnoreCase);

                FileSystemHelpers.SmartCopy(sourcePath,
                                            destinationPath,
                                            previousFiles.Contains,
                                            new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)),
                                            new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)),
                                            path => new DirectoryInfoWrapper(new DirectoryInfo(path)),
                                            skipOldFiles);
            }
            else
            {
                // On first deployment, delete the contents of the destination path before copying
                FileSystemHelpers.DeleteDirectoryContentsSafe(destinationPath);

                // If there's no manifest then there's nothing to copy
                FileSystemHelpers.Copy(sourcePath, destinationPath);
            }
        }

        public static IList<string> GetDeployableProjects(string path, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return (from projectFile in Directory.GetFiles(path, "*.*", searchOption)
                    where IsDeployableProject(projectFile)
                    select projectFile).ToList();
        }

        public static bool IsDeployableProject(string path)
        {
            return _projectFileExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) &&
                    VsHelper.IsWap(path);
        }
    }
}
