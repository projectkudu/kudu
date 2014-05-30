namespace Kudu.Contracts.Jobs
{
    public interface ITriggeredJobsManager : IJobsManager<TriggeredJob>
    {
        void InvokeTriggeredJob(string jobName, string arguments);

        TriggeredJobHistory GetJobHistory(string jobName, string etag, out string currentETag);

        TriggeredJobRun GetJobRun(string jobName, string runId);
    }
}