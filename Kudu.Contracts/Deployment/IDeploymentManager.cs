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
        void Deploy(IRepository repository, ChangeSet changeSet, string deployer, bool clean);
        void Deploy(IRepository repository, string deployer);
        
        /// <summary>
        /// Creates a temporary deployment that is used as a placeholder until changeset details are available.
        /// </summary>
        /// <param name="statusText"></param>
        /// <returns></returns>
        string CreateTemporaryDeployment(string statusText, ChangeSet changeset = null, string deployedBy = null);
        void DeleteTemporaryDeployment(string id = null);

        ILogger GetLogger(string id);
    }
}
