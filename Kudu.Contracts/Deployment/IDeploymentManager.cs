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
        /// Removes the files from the last deployment from wwwroot
        /// Will not remove any file that was added in any other way (during runtime or using direct ftp for example)
        /// </summary>
        void CleanWwwRoot();

        /// <summary>
        /// Creates a temporary deployment that is used as a placeholder until changeset details are available.
        /// </summary>
        /// <param name="statusText"></param>
        /// <returns></returns>
        IDisposable CreateTemporaryDeployment(string statusText, ChangeSet changeset = null, string deployedBy = null);

        ILogger GetLogger(string id);
    }
}
