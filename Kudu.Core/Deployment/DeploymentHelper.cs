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
        private static readonly List<string> _emptyList = Enumerable.Empty<string>().ToList();

        public static void CopyWithManifest(string sourcePath, string destinationPath, IDeploymentManifestReader previousManifest, bool skipOldFiles = true)
        {
            if (previousManifest != null)
            {
                var previousFiles = new HashSet<string>(previousManifest.GetPaths(), StringComparer.OrdinalIgnoreCase);

                SmartCopy(sourcePath, destinationPath, previousFiles.Contains, new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)), new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)), path => new DirectoryInfoWrapper(new DirectoryInfo(path)));
            }
            else
            {
                // On first deployment, delete the contents of the destination path before copying
                FileSystemHelpers.DeleteDirectoryContentsSafe(destinationPath);

                // If there's no manifest then there's nothing to copy
                FileSystemHelpers.Copy(sourcePath, destinationPath);
            }
        }

        public static IList<string> GetProjects(string path, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (!Directory.Exists(path))
            {
                return _emptyList;
            }

            return (from projectFile in Directory.GetFiles(path, "*.*", searchOption)
                    where IsProject(projectFile)
                    select projectFile).ToList();
        }

        public static bool IsProject(string path)
        {
            return _projectFileExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsDeployableProject(string path)
        {
            return IsProject(path) && VsHelper.IsWap(path);
        }

        public static void SmartCopy(string sourcePath, string destinationPath, Func<string, bool> existsInPrevious)
        {
            SmartCopy(sourcePath,
                      destinationPath,
                      existsInPrevious,
                      new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)),
                      new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)),
                      path => new DirectoryInfoWrapper(new DirectoryInfo(path)));
        }


        internal static void SmartCopy(string sourcePath,
                                       string destinationPath,
                                       Func<string, bool> existsInPrevious,
                                       DirectoryInfoBase sourceDirectory,
                                       DirectoryInfoBase destinationDirectory,
                                       Func<string, DirectoryInfoBase> createDirectoryInfo)
        {
            // Skip source control folder
            if (FileSystemHelpers.IsSourceControlFolder(sourceDirectory))
            {
                return;
            }

            if (!destinationDirectory.Exists)
            {
                destinationDirectory.Create();
            }

            // var previousFilesLookup = GetFiles(previousDirectory);
            var destFilesLookup = FileSystemHelpers.GetFiles(destinationDirectory);
            var sourceFilesLookup = FileSystemHelpers.GetFiles(sourceDirectory);

            foreach (var destFile in destFilesLookup.Values)
            {
                // If the file doesn't exist in the source, only delete if:
                // 1. We have no previous directory
                // 2. We have a previous directory and the file exists there

                // Trim the start path
                string previousPath = destFile.FullName.Substring(destinationPath.Length).TrimStart('\\');
                if (!sourceFilesLookup.ContainsKey(destFile.Name) &&
                    ((existsInPrevious == null) ||
                    (existsInPrevious != null &&
                    existsInPrevious(previousPath))))
                {
                    destFile.Delete();
                }
            }

            foreach (var sourceFile in sourceFilesLookup.Values)
            {
                // Skip the .deployment file
                if (sourceFile.Name.Equals(DeploymentConfiguration.DeployConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                // if the file exists in the destination then only copy it again if it's
                // last write time is different than the same file in the source (only if it changed)
                FileInfoBase targetFile;
                if (destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) &&
                    sourceFile.LastWriteTimeUtc == targetFile.LastWriteTimeUtc)
                {
                    continue;
                }

                // Otherwise, copy the file
                string path = FileSystemHelpers.GetDestinationPath(sourcePath, destinationPath, sourceFile);

                OperationManager.Attempt(() => sourceFile.CopyTo(path, overwrite: true));
            }

            var sourceDirectoryLookup = FileSystemHelpers.GetDirectores(sourceDirectory);
            var destDirectoryLookup = FileSystemHelpers.GetDirectores(destinationDirectory);

            foreach (var destSubDirectory in destDirectoryLookup.Values)
            {
                // If the directory doesn't exist in the source, only delete if:
                // 1. We have no previous directory
                // 2. We have a previous directory and the file exists there

                string previousPath = destSubDirectory.FullName.Substring(destinationPath.Length).TrimStart('\\');
                if (!sourceDirectoryLookup.ContainsKey(destSubDirectory.Name) &&
                    ((existsInPrevious == null) ||
                    (existsInPrevious != null &&
                    existsInPrevious(previousPath))))
                {
                    destSubDirectory.Delete(recursive: true);
                }
            }

            foreach (var sourceSubDirectory in sourceDirectoryLookup.Values)
            {
                DirectoryInfoBase targetSubDirectory;
                if (!destDirectoryLookup.TryGetValue(sourceSubDirectory.Name, out targetSubDirectory))
                {
                    string path = FileSystemHelpers.GetDestinationPath(sourcePath, destinationPath, sourceSubDirectory);
                    targetSubDirectory = createDirectoryInfo(path);
                }

                // Sync all sub directories
                SmartCopy(sourcePath, destinationPath, existsInPrevious, sourceSubDirectory, targetSubDirectory, createDirectoryInfo);
            }
        }

    }
}
