using System;
using System.IO;
using System.Linq;
using Kudu.Core.Deployment;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Infrastructure
{
    internal static class AspNet5Helper
    {
        public static bool IsWebApplicationProjectJsonFile(string projectJsonPath)
        {
            try
            {
                using (var reader = new StreamReader(projectJsonPath))
                {
                    var parsedProjectJson = JObject.Parse(reader.ReadToEnd());
                    return parsedProjectJson["webroot"] != null;
                }
            }
            catch
            {
                // Assume not an ASP.NET 5 project if can't parse project.json
                return false;
            }
        }

        public static AspNet5Sdk GetAspNet5Sdk(string rootPath)
        {
            AspNet5Sdk aspNetSdk;
            if (!TryGetAspNet5Sdk(rootPath, out aspNetSdk))
            {
                aspNetSdk = new AspNet5Sdk
                {
                    Architecture = GetDefaultAspNet5RuntimeArchitecture()
                };
            }
            return aspNetSdk;
        }

        public static bool TryAspNet5Project(string rootPath, out string projectJsonPath)
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

        private static bool TryGetAspNet5Sdk(string rootPath, out AspNet5Sdk aspNetSdk)
        {
            aspNetSdk = null;
            var globalJson = Path.Combine(rootPath, "global.json");
            if (FileSystemHelpers.FileExists(globalJson))
            {
                var parsedGlobalJson = JObject.Parse(FileSystemHelpers.ReadAllText(globalJson));
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

        private static string GetDefaultAspNet5RuntimeArchitecture()
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
