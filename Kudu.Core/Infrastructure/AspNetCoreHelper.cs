using System;
using System.IO;
using System.Linq;
using Kudu.Core.Deployment;
using Newtonsoft.Json.Linq;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Infrastructure
{
    internal static class AspNetCoreHelper
    {
        private const string ProjectJson = "project.json";
        public static readonly string[] ProjectJsonLookupList = new string[] { $"*{ProjectJson}" };

        public static bool IsWebApplicationProjectJsonFile(string projectJsonPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectJsonPath);
            var webConfig = Path.Combine(projectDirectory, "web.config");
            var wwwrootDirectory = Path.Combine(projectDirectory, "wwwroot");

            return FileSystemHelpers.FileExists(webConfig) ||
                FileSystemHelpers.DirectoryExists(wwwrootDirectory);
        }

        public static bool TryAspNetCoreWebProject(string rootPath, IFileFinder fileFinder, out string projectJsonPath)
        {
            projectJsonPath = null;
            var projectJsonFiles = fileFinder.ListFiles(rootPath, SearchOption.AllDirectories, ProjectJsonLookupList)
                .Where(path => Path.GetFileName(path).Equals(ProjectJson, StringComparison.OrdinalIgnoreCase))
                .Where(IsWebApplicationProjectJsonFile)
                .ToList();

            if (projectJsonFiles.Any())
            {
                projectJsonPath = projectJsonFiles.First();
                return true;
            }

            return false;
        }
    }
}
