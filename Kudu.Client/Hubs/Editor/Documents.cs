using System.Linq;
using Kudu.Client.Model;
using Kudu.Core.Editor;
using SignalR.Hubs;

namespace Kudu.Client {
    public class Documents : Hub {
        private readonly IEditorFileSystem _fileSystem;

        public Documents(IEditorFileSystem fileSystem) {
            _fileSystem = fileSystem;
        }

        public Project GetStatus() {
            return new Project {
                Name = "Project",
                Files = from path in _fileSystem.GetFiles()
                        select new File {
                            Path = path
                        }
            };
        }

        public string OpenFile(string path) {
            return _fileSystem.ReadAllText(path);
        }

        public void SaveFile(File file) {
            _fileSystem.WriteAllText(file.Path, file.Content);
        }

        public void DeleteFile(string path) {
            _fileSystem.Delete(path);
        }
    }
}