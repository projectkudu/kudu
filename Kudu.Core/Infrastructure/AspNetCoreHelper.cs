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
                // for csproj, need to 1st make sure its dotnet core ==> !projectTypeGuids.Any()
                // 2ndly, look for sign of web project
                // either <PackageReference Include="Microsoft.AspNetCore" Version="..." />
                // for dotnet core project created with 2.0 toolings look for "Microsoft.AspNetCore.All"
                // for preview3 its web.config file
                return !projectTypeGuids.Any() &&
                       (VsHelper.IncludesReferencePackage(projectPath, "Microsoft.AspNetCore") ||
                        VsHelper.IncludesReferencePackage(projectPath, "Microsoft.AspNetCore.All") ||
                        IsWebAppFromFolderStruct(projectPath));
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
