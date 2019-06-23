using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class AggregateContinuousJobsManager : AggregateJobsManagerBase<ContinuousJob>, IContinuousJobsManager
    {
        public AggregateContinuousJobsManager(ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics)
            : base(new ContinuousJobsManager(environment.JobsBinariesPath, traceFactory, environment, settings, analytics),
                  excludedList => new ContinuousJobsManager(environment.SecondaryJobsBinariesPath, traceFactory, environment, settings, analytics, excludedList),
                  settings, environment, traceFactory, Constants.ContinuousPath)
        {
        }

        public void DisableJob(string jobName)
            => GetContinuousWriteJobManager(jobName).DisableJob(jobName);

        public void EnableJob(string jobName)
            => GetContinuousWriteJobManager(jobName).EnableJob(jobName);

        public Task<HttpResponseMessage> HandleRequest(string jobName, string path, HttpRequestMessage request)
            => PrimaryJobManager.HasJob(jobName)
            ? (PrimaryJobManager as IContinuousJobsManager).HandleRequest(jobName, path, request)
            : (SecondaryJobManager as IContinuousJobsManager).HandleRequest(jobName, path, request);

        IContinuousJobsManager GetContinuousWriteJobManager(string jobName) => GetWriteJobManagerForJob(jobName) as IContinuousJobsManager;
    }
}
