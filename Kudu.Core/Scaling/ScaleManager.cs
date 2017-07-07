using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kudu.Core.Scaling
{
    public class ScaleManager : IScaleManager
    {
        private static readonly string _partitionKey;
        private static readonly string _managerKey;

        static ScaleManager()
        {
            // TODO, suwatch:
            string runtimeSiteName = System.Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME") ?? "functiondev200";
            if (runtimeSiteName.StartsWith("~1", StringComparison.OrdinalIgnoreCase))
            {
                runtimeSiteName = runtimeSiteName.Substring(2);
            }

            _partitionKey = runtimeSiteName;
            _managerKey = string.Format("{0}(manager)", runtimeSiteName);
        }

        public async Task<WorkerInfo> GetWorker(string id)
        {
            return (await ListWorkers()).First(w => w.Id == id);
        }

        public async Task<IEnumerable<WorkerInfo>> ListWorkers()
        {
            var workers = await AzureTableUtils.ListWorkers();
            var manager = workers.FirstOrDefault(w =>
            {
                return string.Equals(w.PartitionKey, ScaleManager._managerKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(w.RowKey, ScaleManager._partitionKey, StringComparison.OrdinalIgnoreCase);
            });

            return workers
                .Where(w => string.Equals(w.PartitionKey, ScaleManager._partitionKey, StringComparison.OrdinalIgnoreCase))
                .Select(w => new WorkerInfo
                {
                    Id = w.RowKey,
                    StampName = w.StampName,
                    WorkerName = w.WorkerName,
                    LoadFactor = w.LoadFactor >= int.MaxValue ? "MAX" : (w.LoadFactor <= int.MinValue ? "MIN" : w.LoadFactor.ToString()),
                    LastModifiedTimeUtc = w.Timestamp,
                    IsManager = manager != null && string.Equals(w.StampName, manager.StampName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(w.WorkerName, manager.WorkerName, StringComparison.OrdinalIgnoreCase),
                    IsStale = DateTime.UtcNow > DateTime.Parse(w.Timestamp).ToUniversalTime().AddSeconds(120)
                });
        }
    }
}