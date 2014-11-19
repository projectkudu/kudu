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

        public static bool TryAspNet5Project(string projectDirectory, out string projectJsonPath)
        {
            projectJsonPath = null;
            var projectJsonFiles = Directory.GetFiles(projectDirectory, "project.json", SearchOption.AllDirectories);
            foreach (var filePath in projectJsonFiles.Where(IsWebApplicationProjectJsonFile))
            {
                projectJsonPath = filePath;
                return true;
            }
            return false;
        }
    }
}
