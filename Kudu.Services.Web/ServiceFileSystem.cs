using System.Collections.Generic;
using Kudu.Core.Editor;

namespace Kudu.Services.Web {
    public class ServiceFileSystem : IFileSystem {
        private readonly IFileSystem _fileSystem;

        public ServiceFileSystem(ILocationProvider locationProvider) {
            _fileSystem = new PhysicalFileSystem(locationProvider.RepositoryRoot);
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
    }
}