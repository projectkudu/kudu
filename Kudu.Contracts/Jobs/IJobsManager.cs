using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Contracts.Jobs
{
    public interface IJobsManager<TJob> where TJob : JobBase, new()
    {
        IEnumerable<TJob> ListJobs();

        TJob GetJob(string jobName);

        TJob CreateOrReplaceJobFromZipStream(Stream zipStream, string jobName);

        TJob CreateOrReplaceJobFromFileStream(Stream scriptFileStream, string jobName, string scriptFileName);

        Task DeleteJobAsync(string jobName);

        JobSettings GetJobSettings(string jobName);

        void SetJobSettings(string jobName, JobSettings jobSettings);
    }
}