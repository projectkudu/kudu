using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentManager : KuduRemoteClientBase
    {
        public RemoteDeploymentManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public Task<IEnumerable<DeployResult>> GetResultsAsync(int? maxItems = null, bool excludeFailed = false)
        {
            string url = "?$orderby=ReceivedTime desc";
            if (maxItems != null && maxItems >= 0)
            {
                url += String.Format("&$top={0}", maxItems);
            }
            if (excludeFailed)
            {
                url += "&$filter=LastSuccessEndTime ne null";
            }

            return _client.GetJsonAsync<IEnumerable<DeployResult>>(url);
        }

        public Task<DeployResult> GetResultAsync(string id)
        {
            return _client.GetJsonAsync<DeployResult>(id);
        }

        public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(string id)
        {
            return _client.GetJsonAsync<IEnumerable<LogEntry>>(id + "/log");
        }

        public Task<IEnumerable<LogEntry>> GetLogEntryDetailsAsync(string id, string logId)
        {
            return _client.GetJsonAsync<IEnumerable<LogEntry>>(id + "/log/" + logId);
        }

        public Task DeleteAsync(string id)
        {
            return _client.DeleteSafeAsync(id);
        }

        public Task DeployAsync(string id)
        {
            return _client.PutAsync(id);
        }

        public Task DeployAsync(string id, bool clean)
        {
            var param = new KeyValuePair<string, string>("clean", clean.ToString());
            return _client.PutAsync(id, param);
        }
    }
}
