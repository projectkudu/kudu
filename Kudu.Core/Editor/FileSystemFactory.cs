using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Editor {
    public class FileSystemFactory : IFileSystemFactory {
        private readonly IEnvironment _environment;

        public FileSystemFactory(IEnvironment environment) {
            _environment = environment;
        }

        public IFileSystem CreateFileSystem() {
            // TODO: We need to do some caching here.
            var solutions = VsSolution.GetSolutions(_environment.RepositoryPath);
            if (solutions.All(s => !s.Projects.Any(p => p.IsWap))) {
                // TODO: Detect if editing is enabled
                // If this isn't a wap (Web Application Project), then mirror changes
                // in both repositories.
                return new MirrorRepository(new PhysicalFileSystem(_environment.RepositoryPath),
                                            new PhysicalFileSystem(_environment.DeploymentTargetPath));
            }

            // If we find a solution file then use the solution implementation so only get a subset
            // of the files (ones included in the project)
            if (solutions.Any()) {
                return new SolutionFileSystem(_environment.RepositoryPath, solutions.Select(s => s.Path));
            }

            return new PhysicalFileSystem(_environment.DeploymentTargetPath);
        }
    }
}
