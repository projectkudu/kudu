using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public class GlobalJson
    {
        public Sdk sdk { get; set; }
    }

    public class Sdk
    {
        public String version { get; set; }
    }

    internal static class VsHelper
    {
        public static readonly string[] SolutionsLookupList = new string[] { "*.sln" };

        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        public static IList<VsSolution> GetSolutions(string path, IFileFinder fileFinder, SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<string> filesList = fileFinder.ListFiles(path, searchOption, SolutionsLookupList);
            return filesList.Select(s => new VsSolution(s)).ToList();
        }

        /// <summary>
        /// Locates the solution(s) where the specified project is (search up the tree up to the repository path)
        /// </summary>
        public static IList<VsSolution> FindContainingSolutions(string repositoryPath, string targetPath, IFileFinder fileFinder)
        {
            string solutionsPath = PathUtilityFactory.Instance.CleanPath(targetPath);
            repositoryPath = PathUtilityFactory.Instance.CleanPath(repositoryPath);

            while (solutionsPath != null && solutionsPath.Contains(repositoryPath))
            {
                var solutionsFound = from solution in GetSolutions(solutionsPath, fileFinder, SearchOption.TopDirectoryOnly)
                                     where ExistsInSolution(solution, targetPath)
                                     select solution;

                if (solutionsFound.Any())
                {
                    return solutionsFound.ToList();
                }

                if (PathUtilityFactory.Instance.PathsEquals(solutionsPath, repositoryPath))
                {
                    break;
                }

                var parent = Directory.GetParent(solutionsPath);
                solutionsPath = parent != null ? parent.ToString() : null;
            }

            return new List<VsSolution>();
        }

        /// <summary>
        /// Locates the unambiguous solution matching this project
        /// </summary>
        public static VsSolution FindContainingSolution(string repositoryPath, string targetPath, IFileFinder fileFinder)
        {
            var solutions = FindContainingSolutions(repositoryPath, targetPath, fileFinder);

            // Don't want to use SingleOrDefault since that throws
            if (solutions.Count == 0 || solutions.Count > 1)
            {
                return null;
            }

            return solutions[0];
        }

        public static string GetTargetFramework(string path)
        {
            string document = null;
            try
            {
                document = File.ReadAllText(path);
                return GetTargetFrameworkContents(document);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string GetTargetFrameworkContents(string content)
        {
            try
            {
                XDocument document = XDocument.Parse(content);
                var targetFramework = document.Root.Descendants("TargetFramework");
                var targetFrameworkElement = targetFramework.FirstOrDefault();
                if (targetFrameworkElement != null && targetFrameworkElement.Value != null)
                {
                    KuduEventSource.Log.GenericEvent(
                            ServerConfiguration.GetApplicationName(),
                            string.Format("Dotnet target framework found: {0}", targetFrameworkElement.Value),
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty);
                    return targetFrameworkElement.Value;
                }

                var targetFrameworks = document.Root.Descendants("TargetFrameworks");
                var targetFrameworksElement = targetFrameworks.FirstOrDefault();
                if (targetFrameworksElement != null && targetFrameworksElement.Value != null)
                {
                    KuduEventSource.Log.GenericEvent(
                            ServerConfiguration.GetApplicationName(),
                            string.Format("Multiple Dotnet target frameworks found: {0}. Using First", targetFrameworksElement.Value),
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty);
                    return targetFrameworksElement.Value.Split(';').First();
                }
                // old-style .csproj
                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // Look for `<TargetFramework>` or `<TargetFrameworks>``
        // netcoreapp3.X or netstandard2.1
        public static bool IsDotNetCore3(string target)
        {
            return target.StartsWith("netcoreapp3", StringComparison.OrdinalIgnoreCase) ||
                   target.Equals("netstandard2.1", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetProjectSDK(string path)
        {
            string document = null;
            try
            {
                document = File.ReadAllText(path);
                return GetProjectSDKContent(document);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string GetProjectSDKContent(string content)
        {
            try
            {
                XDocument document = XDocument.Parse(content);
                var projectElement = document.Root;
                if (projectElement != null 
                    && projectElement.Value != null
                    && projectElement.Attribute("Sdk") != null
                    && projectElement.Attribute("Sdk").Value != null)
                {
                    return projectElement.Attribute("Sdk").Value;
                }
                return string.Empty;
            }
            catch(Exception)
            {
                return string.Empty;
            }
        }

        public static Boolean IsAspNetCoreSDK(string sdk)
        {
            return sdk.Equals("Microsoft.NET.Sdk.Web");
        }

        public static void SniffGlobalJson(String path)
        {
            try
            {
                if (File.Exists(path))
                {
                    String json = File.ReadAllText(path);
                    string version = SniffGlobalJsonContents(json);
                    if (!String.IsNullOrEmpty(version))
                    {
                        KuduEventSource.Log.GenericEvent(
                            ServerConfiguration.GetApplicationName(),
                            string.Format("global.json found, pinned sdk version {0}", version),
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty);
                    }
                }
            }
            catch
            {
                // no op
            }
        }

        public static string SniffGlobalJsonContents(String json)
        {
            try
            {
                GlobalJson obj = JsonConvert.DeserializeObject<GlobalJson>(json);
                return obj.sdk.version;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsWap(IEnumerable<Guid> projectTypeGuids)
        {
            return projectTypeGuids.Contains(_wapGuid);
        }

        public static IEnumerable<Guid> GetProjectTypeGuids(string path)
        {
            // only exist in old csprojs
            var projectTypeGuids = GetPropertyValues(path, "ProjectTypeGuids", Csproj.oldFormat);

            var guids = from value in projectTypeGuids
                        from guid in value.Split(';')
                        select new Guid(guid.Trim('{', '}'));
            return guids;
        }

        // takes multiple package names, return true if at least one is presented
        public static bool IncludesAnyReferencePackage(string path, params string[] packageNames)
        {
            var packages = from packageReferences in XDocument.Load(path).Descendants("PackageReference")
                           let packageReferenceName = packageReferences.Attribute("Include")
                           where packageReferenceName != null && packageNames.Contains(packageReferenceName.Value, StringComparer.OrdinalIgnoreCase)
                           select packageReferenceName.Value;

            return packages.Any();
        }

        public static bool IncludesAnyFrameworkReferenceContent(string content, string referenceNames)
        {
            try {
                var packages = from frameworkReferences in XDocument.Parse(content).Descendants("FrameworkReference")
                            let frameworkReferenceName = frameworkReferences.Attribute("Include")
                            where frameworkReferenceName != null && referenceNames.Equals(frameworkReferenceName.Value)
                            select frameworkReferenceName.Value;

                return packages.Any();
            }
            catch(Exception)
            {
                return false;
            }
        }

        public static bool IncludesAnyFrameworkReference(string path, string referenceNames)
        {
            try
            {
                return IncludesAnyFrameworkReferenceContent(File.ReadAllText(path), referenceNames);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static IEnumerable<string> GetPropertyValues(string path, string propertyName, Csproj projectFormat)
        {
            var document = XDocument.Parse(File.ReadAllText(path));
            IEnumerable<string> propertyValues = Enumerable.Empty<string>();

            var root = document.Root;
            if (root == null)
            {
                return propertyValues;
            }

            if (((int)projectFormat & 1) != 0)
            {
                propertyValues = from propertyGroup in document.Root.Elements(GetName("PropertyGroup"))
                                 let property = propertyGroup.Element(GetName(propertyName))
                                 where property != null
                                 select property.Value;

            }
            // if found already, we can return
            // else if the property possibly exists in the new csproj, we run query without namespace
            if (!propertyValues.Any() && ((int)projectFormat & 2) != 0)
            {
                // new csproj does not have a namespace:http://schemas.microsoft.com/developer/msbuild/2003
                propertyValues = from propertyGroup in root.Elements(XName.Get("PropertyGroup"))
                                 let property = propertyGroup.Element(XName.Get(propertyName))
                                 where property != null
                                 select property.Value;
            }

            return propertyValues;
        }

        public static bool IsExecutableProject(string projectPath)
        {
            var outputTypes = GetPropertyValues(projectPath, "OutputType", Csproj.bothFormat);
            return outputTypes.Contains("exe", StringComparer.OrdinalIgnoreCase);
        }

        private static bool ExistsInSolution(VsSolution solution, string targetPath)
        {
            return (from p in solution.Projects
                    where PathUtilityFactory.Instance.PathsEquals(p.AbsolutePath, targetPath)
                    select p).Any();
        }

        private static XName GetName(string name)
        {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }

        // 01, 10, 11 in binares
        public enum Csproj { oldFormat = 1, newFormat, bothFormat }
    }
}
