using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager {
        private readonly string _source;
        private readonly string _destination;

        public DeploymentManager(string source, string destination) {
            _source = source;
            _destination = destination;
        }

        public IDeployer CreateDeployer() {
            return new BasicDeployer(_source, _destination);
        }

        public IEnumerable<DeployResult> GetResults() {
            throw new NotImplementedException();
        }

        public DeployResult GetResult(string id) {
            throw new NotImplementedException();
        }
    }
}
