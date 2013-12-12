using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Jobs;

namespace Kudu.Client.Jobs
{
    public class RemoteJobsManager : KuduRemoteClientBase
    {
        public RemoteJobsManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public Task<IEnumerable<ContinuousJob>> ListContinuousJobsAsync()
        {
            return Client.GetJsonAsync<IEnumerable<ContinuousJob>>("continuous");
        }

        public Task<IEnumerable<TriggeredJob>> ListTriggeredJobsAsync()
        {
            return Client.GetJsonAsync<IEnumerable<TriggeredJob>>("triggered");
        }

        public Task<ContinuousJob> GetContinuousJobAsync(string jobName)
        {
            return Client.GetJsonAsync<ContinuousJob>("continuous/" + jobName);
        }

        public Task<TriggeredJob> GetTriggeredJobAsync(string jobName)
        {
            return Client.GetJsonAsync<TriggeredJob>("triggered/" + jobName);
        }

        public Task<TriggeredJobHistory> GetTriggeredJobHistoryAsync(string jobName)
        {
            return Client.GetJsonAsync<TriggeredJobHistory>("triggered/" + jobName + "/history");
        }

        public Task<TriggeredJobRun> GetTriggeredJobRunAsync(string jobName, string runId)
        {
            return Client.GetJsonAsync<TriggeredJobRun>("triggered/" + jobName + "/history/" + runId);
        }

        public async Task EnableContinuousJobAsync(string jobName)
        {
            await Client.PostAsync("continuous/" + jobName + "/start");
        }

        public async Task DisableContinuousJobAsync(string jobName)
        {
            await Client.PostAsync("continuous/" + jobName + "/stop");
        }

        public async Task SetSingletonContinuousJobAsync(string jobName, bool isSingleton)
        {
            await Client.PostAsync("continuous/" + jobName + "/singleton?isSingleton=" + isSingleton);
        }

        public async Task InvokeTriggeredJobAsync(string jobName)
        {
            await Client.PostAsync("triggered/" + jobName + "/run");
        }
    }
}