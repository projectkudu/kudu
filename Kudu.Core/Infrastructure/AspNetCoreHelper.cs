using System;
using System.IO;
using System.Linq;
using Kudu.Core.Deployment;
using Newtonsoft.Json.Linq;
using Kudu.Core.SourceControl;
using System.Collections.Generic;

namespace Kudu.Core.Infrastructure
{
    internal static class AspNetCoreHelper
    {
        private const string ProjectJson = "project.json";
        public static readonly string[] ProjectJsonLookupList = new string[] { $"*{ProjectJson}" };

        public static bool IsWebAppFromFolderStruct(string projectFilePath)
        {
            // projectFilePath can be project.json, XXX.xproj or XXX.csproj
            var projectDirectory = Path.GetDirectoryName(projectFilePath);
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
                .Where(IsWebAppFromFolderStruct)
                .ToList();

            if (projectJsonFiles.Any())
            {
                projectJsonPath = projectJsonFiles.First();
                return true;
            }

            return false;
        }

        public static bool IsDotnetCoreFromProjectFile(string projectPath, IEnumerable<Guid> projectTypeGuids)
        {
            if (projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                // for dotnet core preview 3 and after
                // if its .csproj, look for <PackageReference Include="Microsoft.AspNetCore" Version="..." />
                return !projectTypeGuids.Any() &&
                (VsHelper.DoesIncludeReferencePackage(projectPath, "Microsoft.AspNetCore")
                || IsWebAppFromFolderStruct(projectPath));
            }
            else if (projectPath.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
            {
                // for dotnet core preview 2 and before 
                return IsWebAppFromFolderStruct(projectPath);
            }
            return false;
        }
    }
}
