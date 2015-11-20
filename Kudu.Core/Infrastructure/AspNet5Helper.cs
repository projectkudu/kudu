using System;
using System.IO;
using System.Linq;
using Kudu.Core.Deployment;
using Newtonsoft.Json.Linq;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Infrastructure
{
    internal static class AspNet5Helper
    {
        public static readonly string[] GlobalJsonLookupList = new string[] { "*global.json" };

        public static bool IsWebApplicationProjectJsonFile(string projectJsonPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectJsonPath);
            var hostingJson = Path.Combine(projectDirectory, "hosting.json");
            var wwwrootDirectory = Path.Combine(projectDirectory, "wwwroot");

            return FileSystemHelpers.FileExists(hostingJson) ||
                FileSystemHelpers.DirectoryExists(wwwrootDirectory);
        }

        public static AspNet5Sdk GetAspNet5Sdk(string rootPath, IFileFinder fileFinder)
        {
            AspNet5Sdk aspNetSdk;
            if (!TryGetAspNet5Sdk(rootPath, fileFinder, out aspNetSdk))
            {
                aspNetSdk = new AspNet5Sdk
                {
                    Architecture = GetDefaultAspNet5RuntimeArchitecture()
                };
            }
            return aspNetSdk;
        }

        public static bool TryAspNet5WebProject(string rootPath, out string projectJsonPath)
        {
            projectJsonPath = null;
            var projectJsonFiles = Directory.GetFiles(rootPath, "project.json", SearchOption.AllDirectories);
            foreach (var filePath in projectJsonFiles.Where(IsWebApplicationProjectJsonFile))
            {
                projectJsonPath = filePath;
                return true;
            }
            return false;
        }

        public static bool TryAspNet5ConsoleAppProject(string rootPath, out string projectJsonPath)
        {
            projectJsonPath = null;
            var projectJsonFile = Path.Combine(rootPath, "project.json");
            if (FileSystemHelpers.FileExists(projectJsonFile))
            {
                projectJsonPath = projectJsonFile;
                return true;
            }
            return false;
        }

        private static bool TryGetAspNet5Sdk(string rootPath, IFileFinder fileFinder, out AspNet5Sdk aspNetSdk)
        {
            aspNetSdk = null;
            var globalJsonFiles = fileFinder.ListFiles(rootPath, SearchOption.AllDirectories, new[] { "*global.json" })
                .Where(path => Path.GetFileName(path).Equals("global.json", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (globalJsonFiles.Count == 1 && FileSystemHelpers.FileExists(globalJsonFiles.First()))
            {
                var parsedGlobalJson = JObject.Parse(FileSystemHelpers.ReadAllText(globalJsonFiles.First()));
                if (parsedGlobalJson["sdk"] != null)
                {
                    aspNetSdk = parsedGlobalJson["sdk"].ToObject<AspNet5Sdk>();
                    if (string.IsNullOrEmpty(aspNetSdk.Architecture))
                    {
                        aspNetSdk.Architecture = GetDefaultAspNet5RuntimeArchitecture();
                    }
                    return true;
                }
            }
            return false;
        }

        public static string GetDefaultAspNet5RuntimeArchitecture()
        {
            var bitness = System.Environment.GetEnvironmentVariable(WellKnownEnvironmentVariables.SiteBitness);
            if (bitness == null)
            {
                return System.Environment.Is64BitProcess ? "x64" : "x86";
            }
            return bitness.Equals(Constants.X64Bit, StringComparison.OrdinalIgnoreCase) ? "x64" : "x86";
        }
    }
}
