namespace Kudu.Contracts.Jobs
{
    public interface IContinuousJobsManager : IJobsManager<ContinuousJob>
    {
        void DisableJob(string jobName);

        void EnableJob(string jobName);

        ContinuousJobSettings GetJobSettings(string jobName);

        void SetJobSettings(string jobName, ContinuousJobSettings jobSettings);
    }
}