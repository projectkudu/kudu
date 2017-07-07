using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.Core.Scaling
{
    public interface IScaleManager
    {
        Task<IEnumerable<WorkerInfo>> ListWorkers();
        Task<WorkerInfo> GetWorker(string id);
    }
}
