using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core {
    public class Environment : IEnvironment {
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        private readonly string _repositoryRoot;
        private readonly string _deployRoot;

        public Environment(string repositoryRoot, string deployRoot) {
            _repositoryRoot = repositoryRoot;
            _deployRoot = deployRoot;
        }

        public bool RequiresBuild {
            get {
                // TODO: Cache this.
                return IsWap();
            }
        }

        public string RepositoryPath {
            get {
                return _repositoryRoot;
            }
        }

        public string DeploymentPath {
            get {
                return _deployRoot;
            }
        }

        public IEnumerable<string> GetWebApplicationProjects() {
            return Directory.EnumerateFiles(_repositoryRoot, "*proj", SearchOption.AllDirectories)
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
    }
}
