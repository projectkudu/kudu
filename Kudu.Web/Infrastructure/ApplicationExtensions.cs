using System.Net;
using System.Threading.Tasks;
using Kudu.Client.SourceControl;
using Kudu.Core.SourceControl;
using Kudu.Web.Models;
using Kudu.Client.Deployment;

namespace Kudu.Web.Infrastructure
{
    public static class ApplicationExtensions
    {
        public static Task<RepositoryInfo> GetRepositoryInfo(this Application application, ICredentials credentials)
        {
            var repositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "live/scm");
            repositoryManager.Credentials = credentials;
            return repositoryManager.GetRepositoryInfo();
        }

        public static RemoteDeploymentManager GetDeploymentManager(this Application application, ICredentials credentials)
        {
            var deploymentManager = new RemoteDeploymentManager(application.ServiceUrl + "/deployments");
            deploymentManager.Credentials = credentials;
            return deploymentManager;
        }
    }
}