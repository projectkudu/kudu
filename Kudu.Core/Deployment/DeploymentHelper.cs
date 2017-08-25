using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment
{
    public static class DeploymentHelper
    {
        // build using msbuild in Azure
        // does not include njsproj(node), pyproj(python), they are built differently
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj", ".fsproj", ".xproj" };

        public static readonly string[] ProjectFileLookup = _projectFileExtensions.Select(p => "*" + p).ToArray();

        public static IList<string> GetMsBuildProjects(string path, IFileFinder fileFinder, SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<string> filesList = fileFinder.ListFiles(path, searchOption, ProjectFileLookup);
            return filesList.ToList();
        }

        public static bool IsMsBuildProject(string path)
        {
            return _projectFileExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsDefaultWebRootContent(string webroot)
        {
            if (!FileSystemHelpers.DirectoryExists(webroot))
            {
                // degenerated
                return true;
            }

            var entries = FileSystemHelpers.GetFileSystemEntries(webroot);
            if (entries.Length == 0)
            {
                // degenerated
                return true;
            }

            if (entries.Length == 1 && FileSystemHelpers.FileExists(entries[0]))
            {
                string hoststarthtml = Path.Combine(webroot, Constants.HostingStartHtml);
                return String.Equals(entries[0], hoststarthtml, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
