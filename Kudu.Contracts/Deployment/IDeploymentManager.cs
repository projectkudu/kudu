using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        Task DeployAsync(IRepository repository, ChangeSet changeSet, string deployer, bool clean, bool needFileUpdate = true);

        /// <summary>
        /// Creates a temporary deployment that is used as a placeholder until changeset details are available.
        /// </summary>
        /// <param name="statusText"></param>
        /// <returns></returns>
        IDisposable CreateTemporaryDeployment(string statusText, out ChangeSet tempChangeSet, ChangeSet changeset = null, string deployedBy = null);

        ILogger GetLogger(string id);
    }
}
