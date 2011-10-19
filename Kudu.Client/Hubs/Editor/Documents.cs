using System;
using System.Linq;
using Kudu.Client.Infrastructure;
using Kudu.Client.Model;
using SignalR.Hubs;
using Kudu.Core.Editor;

namespace Kudu.Client {
    public class Documents : Hub {
        private readonly ISiteConfiguration _siteConfiguration;

        public Documents(ISiteConfiguration siteConfiguration) {
            _siteConfiguration = siteConfiguration;
        }

        public Project GetStatus() {
            var files = GetActiveFileSystem().GetFiles().ToList();
            return new Project {
                Name = Caller.appName,
                DefaultProject = Caller.defaultProject ?? files.FirstOrDefault(path => System.IO.Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase)),
                Files = from path in files
                        select new File {
                            Path = path
                        }
            };
        }

        public string OpenFile(string path) {
            return GetActiveFileSystem().ReadAllText(path);
        }

        public void SaveFile(File file) {
            GetActiveFileSystem().WriteAllText(file.Path, file.Content);
        }

        public void DeleteFile(string path) {
            GetActiveFileSystem().Delete(path);
        }

        private IEditorFileSystem GetActiveFileSystem() {
            string mode = Caller.mode;
            if (String.IsNullOrEmpty(mode)) {
                return _siteConfiguration.FileSystem;
            }
            return _siteConfiguration.DevFileSystem;
        }
    }
}