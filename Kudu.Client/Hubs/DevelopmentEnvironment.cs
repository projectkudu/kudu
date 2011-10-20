using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Client.Infrastructure;
using Kudu.Client.Model;
using Kudu.Core.Editor;
using SignalR.Hubs;

namespace Kudu.Client.Hubs {
    public class DevelopmentEnvironment : Hub {
        private readonly ISiteConfiguration _configuration;
        public DevelopmentEnvironment(ISiteConfiguration configuration) {
            _configuration = configuration;
        }

        private IEditorFileSystem FileSystem {
            get {
                return _configuration.FileSystem;
            }
        }

        public Project GetProject() {
            var files = FileSystem.GetFiles().ToList();
            var projects = from path in files
                           where Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                           select path;

            return new Project {
                Name = Caller.applicationName,
                Projects = projects,
                Files = from path in files
                        select new ProjectFile {
                            Path = path
                        }
            };
        }

        public string OpenFile(string path) {
            return FileSystem.ReadAllText(path);
        }

        public void SaveAllFiles(IEnumerable<ProjectFile> files) {
            IEditorFileSystem fileSystem = FileSystem;
            foreach (var file in files) {
                fileSystem.WriteAllText(file.Path, file.Content);
            }
        }

        public void SaveFile(ProjectFile file) {
            FileSystem.WriteAllText(file.Path, file.Content);
        }
    }
}
