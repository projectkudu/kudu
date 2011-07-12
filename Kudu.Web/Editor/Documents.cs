using System.Linq;
using Kudu.Core.Editor;
using SignalR.Hubs;

namespace Kudu.Web {
    public class Documents : Hub {        
        public Project GetStatus() {
            return new Project {
                Name = "Project",
                Files = from path in GetFileSystem().GetFiles()
                        select new File {
                            Path = path
                        }
            };
        }

        public string OpenFile(string path) {
            return GetFileSystem().ReadAllText(path);
        }

        public void SaveFile(File file) {
            GetFileSystem().WriteAllText(file.Path, file.Content);
        }

        public void DeleteFile(string path) {
            GetFileSystem().Delete(path);
        }

        private IFileSystem GetFileSystem() {
            string path = "http://localhost:52590/files";

            return new RemoteFileSystem(path);
        }
    }
}