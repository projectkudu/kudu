using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core {
    public class Environment : IEnvironment {
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        private readonly string _repositoryPath;
        private readonly string _deployPath;
        private readonly string _buildPath;

        public Environment(string repositoryPath, string deployPath, string buildPath) {
            _repositoryPath = repositoryPath;
            _deployPath = deployPath;
            _buildPath = buildPath;
        }

        public bool RequiresBuild {
            get {
                // TODO: Cache this.
                return IsWap();
            }
        }

        public string RepositoryPath {
            get {
                EnsureDirectory(_repositoryPath);
                return _repositoryPath;
            }
        }

        public string DeploymentPath {
            get {
                EnsureDirectory(_deployPath);
                return _deployPath;
            }
        }

        public string BuildPath {
            get {
                EnsureDirectory(_buildPath);
                return _buildPath;
            }
        }

        public IEnumerable<string> GetWebApplicationProjects() {
            return Directory.EnumerateFiles(_repositoryPath, "*proj", SearchOption.AllDirectories)
                            .Where(path => IsProjectFile(path) && IsProjectFileWap(path));
        }

        private bool IsWap() {
            return GetWebApplicationProjects().Any();
        }

        private static bool IsProjectFile(string path) {
            return _projectFileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsProjectFileWap(string path) {
            var document = XDocument.Parse(File.ReadAllText(path));

            var guids = from propertyGroup in document.Root.Elements(GetName("PropertyGroup"))
                        let projectTypeGuids = propertyGroup.Element(GetName("ProjectTypeGuids"))
                        where projectTypeGuids != null
                        from guid in projectTypeGuids.Value.Split(';')
                        select new Guid(guid.Trim('{', '}'));

            return guids.Contains(_wapGuid);
        }

        private static XName GetName(string name) {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }

        private static void EnsureDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }
    }
}
