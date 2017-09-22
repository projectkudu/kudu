using System;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public interface IFetchDeploymentManager
    {
        Task<FetchDeploymentRequestResult> FetchDeploy(
            DeploymentInfo deployInfo,
            bool asyncRequested,
            Uri requestUri,
            string targetBranch);
    }
}
