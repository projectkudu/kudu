using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment {
    public interface IDeploymentManager {
        string ActiveDeploymentId { get; }

        event Action<DeployResult> StatusChanged;

        IEnumerable<DeployResult> GetResults();
        DeployResult GetResult(string id);
        IEnumerable<LogEntry> GetLogEntries(string id);
        void Build(string id);
        void Deploy(string id);
        void Deploy();
    }
}
