using System;
using System.Collections.Generic;
using Kudu.Core.SourceControl;

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
        
        /// <summary>
        /// Creates a permanent deployment for the changeset
        /// </summary>
        /// <param name="statusText"></param>
        void CreateDeployment(ChangeSet changeset, string deployedBy, string statusText);

        /// <summary>
        /// Creates a temporary deployment that is used as a placeholder until changeset details are available.
        /// </summary>
        /// <param name="statusText"></param>
        /// <returns></returns>
        IDisposable CreateTemporaryDeployment(string statusText);
    }
}
