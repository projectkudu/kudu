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
                // Common Project System-style no longer has projectTypeGuid
                if (projectTypeGuids.Any())
                {
                    return false;
                }

                // ASP.NET CORE 2.2 and lower requires these
                // either <PackageReference Include="Microsoft.AspNetCore" Version="..." />
                // for dotnet core project created with 2.0 toolings look for "Microsoft.AspNetCore.All"
                // for dotnet core project created with 2.1 toolings look for "Microsoft.AspNetCore.App"
                if (VsHelper.IncludesAnyReferencePackage(projectPath, "Microsoft.AspNetCore", "Microsoft.AspNetCore.All", "Microsoft.AspNetCore.App"))
                {
                    return true;
                }

                // look for web.config or wwwroot
                if (IsWebAppFromFolderStruct(projectPath))
                {
                    return true;
                }

                // check for <Project Sdk="Microsoft.NET.Sdk.Web"> 
                return VsHelper.IsAspNetCoreSDK(VsHelper.GetProjectSDK(projectPath));
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
