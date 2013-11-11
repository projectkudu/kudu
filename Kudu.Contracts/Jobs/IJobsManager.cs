using System.Collections.Generic;

namespace Kudu.Contracts.Jobs
{
    public interface IJobsManager<TJob> where TJob : JobBase, new()
    {
        IEnumerable<TJob> ListJobs();
        TJob GetJob(string jobName);
    }
}