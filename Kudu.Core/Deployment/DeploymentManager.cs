using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IDeployerFactory _deployerFactory;

        public DeploymentManager(IRepositoryManager repositoryManager,
                                 IDeployerFactory deployerFactory) {
            _repositoryManager = repositoryManager;
            _deployerFactory = deployerFactory;
        }

        public IEnumerable<DeployResult> GetResults() {
            throw new NotImplementedException();
        }

        public DeployResult GetResult(string id) {
            throw new NotImplementedException();
        }

        public void Deploy(string id) {
            IDeployer deployer = _deployerFactory.CreateDeployer();
            deployer.Deploy(id);
        }

        public void Deploy() {
            var repository = _repositoryManager.GetRepository();
            
            if (repository == null) {
                return;
            }

            var activeBranch = repository.GetBranches().FirstOrDefault(b => b.Active);
            string id = repository.CurrentId;

            if (activeBranch != null) {
                repository.Update(activeBranch.Name);
            }
            else {
                repository.Update(id);
            }

            Deploy(id);
        }
    }
}
