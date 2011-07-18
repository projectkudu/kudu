using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager {
        private readonly IRepository _repository;
        private readonly IDeployerFactory _deployerFactory;

        public DeploymentManager(IRepositoryManager repositoryManager, 
                                 IDeployerFactory deployerFactory) {
            _repository = repositoryManager.GetRepository();
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
            var activeBranch = _repository.GetBranches().FirstOrDefault(b => b.Active);
            string id = _repository.CurrentId;

            if (activeBranch != null) {
                _repository.Update(activeBranch.Name);
            }
            else {
                _repository.Update(id);
            }

            Deploy(id);
        }
    }
}
