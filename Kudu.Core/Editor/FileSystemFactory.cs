using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Editor {
    public class FileSystemFactory : IFileSystemFactory {
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        private readonly string _repositoryRoot;
        private readonly string _deployRoot;
        
        public FileSystemFactory(string repositoryRoot, string deployRoot) {
            _repositoryRoot = repositoryRoot;
            _deployRoot = deployRoot;
        }

        public IFileSystem CreateFileSystem() {
            // TODO: We need to do some caching here.

            if (!IsWap()) {
                // TODO: Detect if editing is enabled
                // If this isn't a wap (Web Application Project), then mirror changes
                // in both repositories.
                return new MirrorRepository(new PhysicalFileSystem(_repositoryRoot),
                                            new PhysicalFileSystem(_deployRoot));
            }

            // If we find a solution file then use the solution implementation so only get a subset
            // of the files (ones included in the project)
            if (Directory.EnumerateFiles(_repositoryRoot, "*.sln").Any()) {
                return new SolutionFileSystem(_repositoryRoot);
            }

            return new PhysicalFileSystem(_repositoryRoot);
        }

        private bool IsWap() {
            return Directory.EnumerateFiles(_repositoryRoot, "*proj", SearchOption.AllDirectories)
                            .Any(path => IsProjectFile(path) && IsProjectFileWap(path));
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
