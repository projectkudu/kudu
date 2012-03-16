using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ionic.Zip;
using Kudu.Client.Deployment;
using Kudu.Client.SourceControl;
using Kudu.Contracts.Infrastructure;
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

        public static Task<XDocument> DownloadTrace(this IApplication application, ICredentials credentials)
        {
            var client = new HttpClient(new HttpClientHandler()
            {
                Credentials = credentials
            });


            return client.GetAsync(application.ServiceUrl + "dump").Then(response =>
            {
                return response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync().Then(stream =>
                {
                    using (var zip = ZipFile.Read(stream))
                    {
                        foreach (var entry in zip)
                        {
                            if (entry.FileName.EndsWith("trace.xml", StringComparison.OrdinalIgnoreCase))
                            {
                                return XDocument.Load(entry.OpenReader());
                            }
                        }
                    }

                    return null;
                });                
            });
        }        
    }
}