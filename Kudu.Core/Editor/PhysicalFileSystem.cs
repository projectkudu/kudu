using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kudu.Core.Editor {
    public class PhysicalFileSystem : IEditorFileSystem {
        private readonly string _root;

        public PhysicalFileSystem(string root) {
            _root = root;
        }

        private string GetFullPath(string path) {
            string normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(_root, path);
        }

        public string ReadAllText(string path) {
            return File.ReadAllText(GetFullPath(path));
        }

        public virtual IEnumerable<string> GetFiles() {
            var directory = new DirectoryInfo(_root);
            return GetFiles(directory);
        }

        private IEnumerable<string> GetFiles(DirectoryInfo directory) {
            if (directory.Name.StartsWith(".") ||
                directory.Attributes.HasFlag(FileAttributes.Hidden)) {
                yield break;
            }

            foreach (var file in directory.EnumerateFiles()) {
                yield return MakeRelative(file.FullName);
            }

            foreach (var subDirectory in directory.EnumerateDirectories()) {
                foreach (var file in GetFiles(subDirectory)) {
                    yield return file;
                }
            }
        }

        public void WriteAllText(string path, string text) {
            File.WriteAllText(GetFullPath(path), text);
        }

        public void Delete(string path) {
            File.Delete(GetFullPath(path));
        }

        protected string MakeRelative(string path) {
            return path.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}