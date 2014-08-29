using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Client.SourceControl;
using Kudu.Core.SourceControl;
using Kudu.Web.Models;
using System.IO;

namespace Kudu.Web.Infrastructure
{
    public static class ApplicationExtensions
    {
        public static Task<RepositoryInfo> GetRepositoryInfo(this IApplication application, ICredentials credentials)
        {
            var repositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "api/scm", credentials);
            return repositoryManager.GetRepositoryInfo();
        }

        public static RemoteDeploymentManager GetDeploymentManager(this IApplication application, ICredentials credentials)
        {
            var deploymentManager = new RemoteDeploymentManager(application.ServiceUrl + "api/deployments", credentials);
            return deploymentManager;
        }

        public static RemoteFetchManager GetFetchManager(this IApplication application, ICredentials credentials)
        {
            return new RemoteFetchManager(application.ServiceUrl + "deploy", credentials);
        }

        public static RemoteDeploymentSettingsManager GetSettingsManager(this IApplication application, ICredentials credentials)
        {
            var deploymentSettingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl + "api/settings", credentials);
            return deploymentSettingsManager;
        }

        public static async Task<XDocument> DownloadTrace(this IApplication application, ICredentials credentials)
        {
            using (var clientHandler = HttpClientHelper.CreateClientHandler(application.ServiceUrl, credentials))
            {
                using (var client = new HttpClient(clientHandler))
                {
                    HttpResponseMessage response = await client.GetAsync(application.ServiceUrl + "api/dump");

                    Stream stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync();

                    return ZipHelper.ExtractTrace(stream);
                }
            }
        }
    }
}
