using System;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public interface IFetchDeploymentManager
    {
        Task<FetchDeploymentRequestResult> DoDeployment(
            DeploymentInfo deployInfo,
            bool asyncRequested,
            Uri requestUri,
            string targetBranch);
    }
}
