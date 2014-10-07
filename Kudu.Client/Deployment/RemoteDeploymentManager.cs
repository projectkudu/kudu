using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentManager : KuduRemoteClientBase
    {
        public RemoteDeploymentManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public Task<IEnumerable<DeployResult>> GetResultsAsync()
        {
            return Client.GetJsonAsync<IEnumerable<DeployResult>>(BuildUrl());
        }

        public Task<DeployResult> GetResultAsync(string id)
        {
            return Client.GetJsonAsync<DeployResult>(BuildUrl(id));
        }

        public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(string id)
        {
            return Client.GetJsonAsync<IEnumerable<LogEntry>>(BuildUrl(id, "log"));
        }

        public Task<IEnumerable<LogEntry>> GetLogEntryDetailsAsync(string id, string logId)
        {
            return Client.GetJsonAsync<IEnumerable<LogEntry>>(BuildUrl(id, "log", logId));
        }

        public async Task<Stream> GetDeploymentScriptAsync()
        {
            HttpResponseMessage response = await Client.GetAsync("deploymentscript");

            return response
                .EnsureSuccessful()
                .Content
                .ReadAsStreamAsync()
                .Result; 
        }

        public Task DeleteAsync(string id)
        {
            return Client.DeleteSafeAsync(BuildUrl(id));
        }

        public Task<HttpResponseMessage> DeployAsync(string id)
        {
            return Client.PutAsync(BuildUrl(id));
        }

        public Task<HttpResponseMessage> DeployWithoutEnsureSuccessfulAsync(string id)
        {
            return Client.PutAsync(BuildUrl(id), ensureSuccessful: false);
        }

        public Task DeployAsync(string id, bool clean)
        {
            var param = new KeyValuePair<string, string>("clean", clean.ToString());
            return Client.PutAsync(BuildUrl(id), param);
        }

        private static string BuildUrl(params string[] parameters)
        {
            return "deployments/" + String.Join("/", parameters);
        }
    }
}
