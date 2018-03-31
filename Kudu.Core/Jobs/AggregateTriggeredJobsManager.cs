using System;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Hooks;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class AggregateTriggeredJobsManager : AggregateJobsManagerBase<TriggeredJob>, ITriggeredJobsManager
    {
        public AggregateTriggeredJobsManager(ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics, IWebHooksManager hooksManager)
            : base(new TriggeredJobsManager(environment.JobsBinariesPath, traceFactory, environment, settings, analytics, hooksManager),
                  excludedList => new TriggeredJobsManager(environment.SecondaryJobsBinariesPath, traceFactory, environment, settings, analytics, hooksManager, excludedList),
                  settings, environment, traceFactory, Constants.TriggeredPath)
        {
        }

        public TriggeredJobHistory GetJobHistory(string jobName, string etag, out string currentETag)
            => GetManagerForJob(jobName).GetJobHistory(jobName, etag, out currentETag);

        public TriggeredJobRun GetJobRun(string jobName, string runId)
            => GetManagerForJob(jobName).GetJobRun(jobName, runId);

        public TriggeredJobRun GetLatestJobRun(string jobName)
            => GetManagerForJob(jobName).GetLatestJobRun(jobName);

        public Uri InvokeTriggeredJob(string jobName, string arguments, string trigger)
            => GetManagerForJob(jobName).InvokeTriggeredJob(jobName, arguments, trigger);

        private ITriggeredJobsManager GetManagerForJob(string jobName)
            => PrimaryJobManager.HasJob(jobName)
            ? PrimaryJobManager as ITriggeredJobsManager
            : SecondaryJobManager as ITriggeredJobsManager;
    }
}
