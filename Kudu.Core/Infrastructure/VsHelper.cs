using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Infrastructure
{
    internal static class VsHelper
    {
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");
        private static readonly List<VsSolution> _noSolutions = new List<VsSolution>();

        public static IList<VsSolution> GetSolutions(string path)
        {
            if (!Directory.Exists(path))
            {
                return _noSolutions;
            }

            return (from solutionFile in Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories)
                    select new VsSolution(solutionFile)).ToList();
        }

        /// <summary>
        /// Locates the solutin(s) where the specified project is
        /// </summary>
        public static IList<VsSolution> FindContainingSolutions(string searchPath, string targetPath)
        {
            return (from solution in GetSolutions(searchPath)
                    where ExistsInSolution(solution, targetPath)
                    select solution).ToList();
        }

        /// <summary>
        /// Locates the unambiguous solution matching this project
        /// </summary>
        public static VsSolution FindContainingSolution(string searchPath, string targetPath)
        {
            var solutions = FindContainingSolutions(searchPath, targetPath);

            // Don't want to use SingleOrDefault since that throws
            if (solutions.Count == 0 || solutions.Count > 1)
            {
                return null;
            }

            return solutions[0];
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

        private static bool ExistsInSolution(VsSolution solution, string targetPath)
        {
            return (from p in solution.Projects
                    where NormalizePath(p.AbsolutePath).Equals(NormalizePath(targetPath))
                    select p).Any();
        }

        private static XName GetName(string name)
        {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).ToUpperInvariant();
        }
    }
}
