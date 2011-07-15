using System.Collections.Generic;

namespace Kudu.Core.Deployment {
    public interface IDeploymentManager {
        IDeployer CreateDeployer();
        IEnumerable<DeployResult> GetResults();
        DeployResult GetResult(string id);
    }
}
