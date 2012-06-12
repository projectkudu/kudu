using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kudu.Client.Deployment;
using Kudu.Client.SourceControl;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.Web.Models;


namespace Kudu.Web.Infrastructure
{
    public static class SiteExtensions
    {
        public static Task<RepositoryInfo> GetRepositoryInfo(this Site site, ICredentials credentials)
        {
            var repositoryManager = new RemoteRepositoryManager(site.ServiceUrl + "live/scm");
            repositoryManager.Credentials = credentials;
            return repositoryManager.GetRepositoryInfo();
        }

        public static RemoteDeploymentManager GetDeploymentManager(this Site site, ICredentials credentials)
        {
            var deploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "/deployments");
            deploymentManager.Credentials = credentials;
            return deploymentManager;
        }

        public static RemoteDeploymentSettingsManager GetSettingsManager(this Site site, ICredentials credentials)
        {
            var deploymentSettingsManager = new RemoteDeploymentSettingsManager(site.ServiceUrl);
            deploymentSettingsManager.Credentials = credentials;
            return deploymentSettingsManager;
        }

        public static Task<XDocument> DownloadTrace(this Site site, ICredentials credentials)
        {
            var client = new HttpClient(new HttpClientHandler()
            {
                Credentials = credentials
            });


            return client.GetAsync(site.ServiceUrl + "dump").Then(response =>
            {
                return response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync().Then(stream =>
                {
                    return ZipHelper.ExtractTrace(stream);
                });
            });
        }
    }
}