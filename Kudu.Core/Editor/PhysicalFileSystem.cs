using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kudu.Core.Editor {
    public class PhysicalFileSystem : IFileSystem {        
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
            return Directory.GetFiles(_root, "*.*", SearchOption.AllDirectories)
                            .Select(MakeRelative);
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