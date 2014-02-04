using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Contracts.Jobs
{
    public interface IJobsManager<TJob> where TJob : JobBase, new()
    {
        IEnumerable<TJob> ListJobs();

        TJob GetJob(string jobName);

        TJob CreateJobFromZipStream(Stream zipStream, string jobName);

        TJob CreateJobFromFileStream(Stream scriptFileStream, string jobName, string scriptFileName);

        Task DeleteJobAsync(string jobName);
    }
}