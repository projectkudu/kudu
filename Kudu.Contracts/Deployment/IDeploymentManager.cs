using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManager
    {
        string ActiveDeploymentId { get; }

        event Action<DeployResult> StatusChanged;

        IEnumerable<DeployResult> GetResults();
        DeployResult GetResult(string id);
        IEnumerable<LogEntry> GetLogEntries(string id);
        IEnumerable<LogEntry> GetLogEntryDetails(string id, string entryId);
        IEnumerable<string> GetManifest(string id);
        void Delete(string id);
        void Deploy(string id);
        void Deploy();
    }
}
