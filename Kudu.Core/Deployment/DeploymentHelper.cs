using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public static class DeploymentHelper
    {
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };
        private static readonly List<string> _emptyList = Enumerable.Empty<string>().ToList();

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
    }
}
