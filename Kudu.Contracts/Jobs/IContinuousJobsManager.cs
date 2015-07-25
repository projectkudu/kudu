using System.Net.Http;
using System.Threading.Tasks;

namespace Kudu.Contracts.Jobs
{
    public interface IContinuousJobsManager : IJobsManager<ContinuousJob>
    {
        void DisableJob(string jobName);

        void EnableJob(string jobName);

        Task<HttpResponseMessage> HandleRequest(string jobName, string path, HttpRequestMessage request);
    }
}