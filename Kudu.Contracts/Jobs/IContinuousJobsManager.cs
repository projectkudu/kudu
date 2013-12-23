namespace Kudu.Contracts.Jobs
{
    public interface IContinuousJobsManager : IJobsManager<ContinuousJob>
    {
        void DisableJob(string jobName);

        void EnableJob(string jobName);

        void SetSingleton(string jobName, bool isSingleton);
    }
}