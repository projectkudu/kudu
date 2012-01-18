using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public static class DeploymentHelpers
    {
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
                // If there's no manifest then there's nothing to copy
                FileSystemHelpers.Copy(sourcePath, destinationPath);
            }
        }
    }
}
