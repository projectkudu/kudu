using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace Kudu.Core.Infrastructure
{
    internal static class VsHelper
    {
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };

        public static IList<VsSolution> GetSolutions(string path)
        {
            return (from solutionFile in Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories)
                    select new VsSolution(solutionFile)).ToList();
        }

        public static IList<string> GetDeployableProjects(string path)
        {
            return (from projectFile in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                    where _projectFileExtensions.Any(extension => projectFile.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) && IsWap(projectFile)
                    select projectFile).ToList();
        }

        public static bool IsWap(string projectPath)
        {
            return IsWap(GetProjectTypeGuids(projectPath));
        }

        public static bool IsWap(IEnumerable<Guid> projectTypeGuids)
        {
            return projectTypeGuids.Contains(_wapGuid);
        }

        public static IEnumerable<Guid> GetProjectTypeGuids(string path)
        {
            var document = XDocument.Parse(File.ReadAllText(path));

            var guids = from propertyGroup in document.Root.Elements(GetName("PropertyGroup"))
                        let projectTypeGuids = propertyGroup.Element(GetName("ProjectTypeGuids"))
                        where projectTypeGuids != null
                        from guid in projectTypeGuids.Value.Split(';')
                        select new Guid(guid.Trim('{', '}'));
            return guids;
        }

        private static XName GetName(string name)
        {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }
    }
}
