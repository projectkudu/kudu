using System;
using System.Collections.Generic;

namespace Kudu.Core.Editor {
    public class MirrorRepository : IEditorFileSystem {
        private readonly IEditorFileSystem _repositoryFileSystem;
        private readonly IEditorFileSystem _deploymentFileSystem;

        public MirrorRepository(IEditorFileSystem repositoryFileSystem, IEditorFileSystem deploymentFileSystem) {
            _repositoryFileSystem = repositoryFileSystem;
            _deploymentFileSystem = deploymentFileSystem;
        }

        public string ReadAllText(string path) {
            return _repositoryFileSystem.ReadAllText(path);
        }

        public IEnumerable<string> GetFiles() {
            return _repositoryFileSystem.GetFiles();
        }

        public void WriteAllText(string path, string content) {
            _repositoryFileSystem.WriteAllText(path, content);
            _deploymentFileSystem.WriteAllText(path, content);
        }

        public void Delete(string path) {
            _repositoryFileSystem.Delete(path);
            _deploymentFileSystem.Delete(path);
        }
    }
}
