using System.IO;
using System.Linq;

namespace Kudu.Core.Editor {
    public class FileSystemFactory : IFileSystemFactory {
        private readonly string _path;

        public FileSystemFactory(string path) {
            _path = path;
        }

        public IFileSystem CreateFileSystem() {
            // If we find a solution file then use the solution implementation so only get a subset
            // of the files (ones included in the project)
            if (Directory.EnumerateFiles(_path, "*.sln").Any()) {
                return new SolutionFileSystem(_path);
            }

            return new PhysicalFileSystem(_path);
        }
    }
}
