using System.Collections.Generic;
using Kudu.Core.Editor;
using System.IO;
using System.Linq;

namespace Kudu.Services.Web {
    public class ServiceFileSystem : IFileSystem {
        private readonly IFileSystem _fileSystem;

        public ServiceFileSystem(ILocationProvider locationProvider) {
            _fileSystem = GetFileSystem(locationProvider);
        }

        public string ReadAllText(string path) {
            return _fileSystem.ReadAllText(path);
        }

        public IEnumerable<string> GetFiles() {
            return _fileSystem.GetFiles();
        }

        public void WriteAllText(string path, string content) {
            _fileSystem.WriteAllText(path, content);
        }

        public void Delete(string path) {
            _fileSystem.Delete(path);
        }

        private IFileSystem GetFileSystem(ILocationProvider locationProvider) {
            // If we find a solution file then use the vs implementation so only get a subset
            // of the files (ones included in the project)
            if (Directory.EnumerateFiles(locationProvider.RepositoryRoot, "*.sln").Any()) {
                return new VsFileSystem(locationProvider.RepositoryRoot);
            }

            return new PhysicalFileSystem(locationProvider.RepositoryRoot);
        }
    }
}