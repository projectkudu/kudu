using System;
using System.Collections.Generic;
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
            return Client.GetJsonAsync<IEnumerable<DeployResult>>("");
        }

        public Task<DeployResult> GetResultAsync(string id)
        {
            return Client.GetJsonAsync<DeployResult>(id);
        }

        public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(string id)
        {
            return Client.GetJsonAsync<IEnumerable<LogEntry>>(id + "/log");
        }

        public Task<IEnumerable<LogEntry>> GetLogEntryDetailsAsync(string id, string logId)
        {
            return Client.GetJsonAsync<IEnumerable<LogEntry>>(id + "/log/" + logId);
        }

        public Task DeleteAsync(string id)
        {
            return Client.DeleteSafeAsync(id);
        }

        public Task DeployAsync(string id)
        {
            return Client.PutAsync(id);
        }

        public Task DeployAsync(string id, bool clean)
        {
            var param = new KeyValuePair<string, string>("clean", clean.ToString());
            return Client.PutAsync(id, param);
        }
    }
}
