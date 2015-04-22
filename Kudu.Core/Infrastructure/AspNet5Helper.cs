using System;
using System.IO;
using System.Linq;
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

        public static string GetAspNet5RuntimeVersion(string rootPath)
        {
            var runtimeVersion = Constants.DnxDefaultVersion;
            var globalJson = Path.Combine(rootPath, "global.json");
            if (File.Exists(globalJson))
            {
                using (var reader = new StreamReader(globalJson))
                {
                    var parsedGlobalJson = JObject.Parse(reader.ReadToEnd());
                    if (parsedGlobalJson["sdk"] != null &&
                        parsedGlobalJson["sdk"]["version"] != null)
                    {
                        runtimeVersion = parsedGlobalJson["sdk"]["version"].ToString();
                    }
                }
            }
            return runtimeVersion;
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
    }
}
