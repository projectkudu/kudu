using System.IO;
using System.Linq;

namespace Kudu.Core.Editor {
    public class FileSystemFactory : IFileSystemFactory {
        private readonly IEnvironment _environment;

        public FileSystemFactory(IEnvironment environment) {
            _environment = environment;
        }

        public IFileSystem CreateFileSystem() {
            // TODO: We need to do some caching here.

            if (!_environment.RequiresBuild) {
                // TODO: Detect if editing is enabled
                // If this isn't a wap (Web Application Project), then mirror changes
                // in both repositories.
                return new MirrorRepository(new PhysicalFileSystem(_environment.RepositoryPath),
                                            new PhysicalFileSystem(_environment.DeploymentTargetPath));
            }

            // If we find a solution file then use the solution implementation so only get a subset
            // of the files (ones included in the project)
            var solutionFiles = Directory.EnumerateFiles(_environment.RepositoryPath, "*.sln", SearchOption.AllDirectories)
                                         .ToList();
            if (solutionFiles.Any()) {
                return new SolutionFileSystem(_environment.RepositoryPath, solutionFiles);
            }

            return new PhysicalFileSystem(_environment.DeploymentTargetPath);
        }
    }
}
