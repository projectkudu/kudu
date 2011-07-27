using System;
using System.Linq;
using System.IO;

namespace Kudu.Core.Deployment {
    public class DeployerFactory : IDeployerFactory {
        private readonly IEnvironment _environment;
        public DeployerFactory(IEnvironment environment) {
            _environment = environment;
        }

        public IDeployer CreateDeployer() {            
            if (_environment.RequiresBuild) {
                // TODO: Have some convention (or setting to determine the deployment project if more than one)
                string projectFile = _environment.GetWebApplicationProjects().FirstOrDefault();

                return new MSBuildDeployer(_environment.RepositoryPath, GetSolutionPath(projectFile), projectFile);
            }

            return new BasicDeployer(_environment.RepositoryPath);
        }        

        private string GetSolutionPath(string projectPath) {
            string path = projectPath;

            while (!_environment.RepositoryPath.Equals(path, StringComparison.OrdinalIgnoreCase)) {
                path = Path.GetDirectoryName(path);
                var solutionFiles = Directory.EnumerateFiles(path, "*.sln").ToList();
                if (solutionFiles.Any()) {
                    // TODO: Ensure that this project is in this solution

                    // Add the trailing slash
                    return solutionFiles.First();
                }
            }

            return null;
        }
    }
}
