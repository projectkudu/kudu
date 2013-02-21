using System;
using System.Collections.Generic;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManager
    {
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
        IDisposable CreateTemporaryDeployment(string statusText, ChangeSet changeset = null, string deployedBy = null);

        // TODO, suwatch: we need a separate IDeploymentStatusManager to interact 
        // with Deploy Status file.   Currently all logics baked into DeploymentManager 
        // and pollute the interface.  Too much churn for S20.  Just patching it now.
        ILogger GetLogger(string id);
        void MarkFailed(string id);
        void UpdateMessage(string id, string message);
        void MarkSuccess(string id);
    }
}
