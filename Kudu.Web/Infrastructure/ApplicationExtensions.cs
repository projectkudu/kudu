using System.Net;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Client.SourceControl;
using Kudu.Core.SourceControl;
using Kudu.Web.Models;

namespace Kudu.Web.Infrastructure
{
    public static class ApplicationExtensions
    {
        public static Task<RepositoryInfo> GetRepositoryInfo(this IApplication application, ICredentials credentials)
        {
            var repositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "live/scm");
            repositoryManager.Credentials = credentials;
            return repositoryManager.GetRepositoryInfo();
        }

        public static RemoteDeploymentManager GetDeploymentManager(this IApplication application, ICredentials credentials)
        {
            var deploymentManager = new RemoteDeploymentManager(application.ServiceUrl + "/deployments");
            deploymentManager.Credentials = credentials;
            return deploymentManager;
        }

        public static RemoteDeploymentSettingsManager GetSettingsManager(this IApplication application, ICredentials credentials)
        {
            var deploymentSettingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl);
            deploymentSettingsManager.Credentials = credentials;
            return deploymentSettingsManager;
        }
    }
}