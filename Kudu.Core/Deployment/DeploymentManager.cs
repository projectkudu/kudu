using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager, IDeployerFactory {
        private readonly IEnvironment _environment;

        public DeploymentManager(IEnvironment environment) {
            _environment = environment;
        }

        public IDeployer CreateDeployer() {
            if (_environment.RequiresBuild) {
                return new MSBuildDeployer(_environment.RepositoryPath,
                                           _environment.DeploymentPath);
            }

            return new BasicDeployer(_environment.RepositoryPath,
                                     _environment.DeploymentPath);
        }

        public IEnumerable<DeployResult> GetResults() {
            throw new NotImplementedException();
        }

        public DeployResult GetResult(string id) {
            throw new NotImplementedException();
        }
    }
}
