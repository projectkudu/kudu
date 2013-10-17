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
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };

        public static readonly string[] ProjectFileLookup = _projectFileExtensions.Select(p => "*" + p).ToArray();

        public static IList<string> GetProjects(string path, IFileFinder fileFinder, SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<string> filesList = fileFinder.ListFiles(path, searchOption, ProjectFileLookup);
            return filesList.ToList();
        }

        public static bool IsProject(string path)
        {
            return _projectFileExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsDeployableProject(string path)
        {
            return IsProject(path) &&
                   (VsHelper.IsWap(path) || VsHelper.IsExecutableProject(path));
        }
    }
}
