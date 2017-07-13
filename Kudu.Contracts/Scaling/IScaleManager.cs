using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kudu.Core.Scaling
{
    public interface IScaleManager
    {
        Task<IEnumerable<WorkerInfo>> ListWorkers();
        Task<WorkerInfo> GetWorker(string id);
        Task UpdateWorker(string id, WorkerInfo info);

        Task<HttpResponseMessage> PingWorker(string id);
        Task<HttpResponseMessage> AddWorker(string id);
        Task<HttpResponseMessage> RemoveWorker(string id);
    }
}
