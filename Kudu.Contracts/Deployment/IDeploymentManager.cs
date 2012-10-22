using Kudu.Contracts.Tracing;
using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManager
    {
        event Action<DeployResult> StatusChanged;

        IEnumerable<DeployResult> GetResults();
        DeployResult GetResult(string id);
        IEnumerable<LogEntry> GetLogEntries(string id);
        IEnumerable<LogEntry> GetLogEntryDetails(string id, string logId);
        void Delete(string id);
        void Deploy(string id, string deployer, bool clean);
        void Deploy(string deployer);
        void CreateExistingDeployment(string id, string deployer);
        IDisposable CreateTemporaryDeployment(string statusText);
    }
}
