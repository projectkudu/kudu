namespace Kudu.Contracts.Jobs
{
    public interface ITriggeredJobsManager : IJobsManager<TriggeredJob>
    {
        System.Uri InvokeTriggeredJob(string jobName, string arguments, string trigger);

        TriggeredJobHistory GetJobHistory(string jobName, string etag, out string currentETag);

        TriggeredJobRun GetJobRun(string jobName, string runId);

        TriggeredJobRun GetLatestJobRun(string jobName);

        string JobsBinariesPath { get; }
    }
}