using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Core.SourceControl;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManager
    {
        IEnumerable<DeployResult> GetResults();
        DeployResult GetResult(string id);
        IEnumerable<LogEntry> GetLogEntries(string id);
        IEnumerable<LogEntry> GetLogEntryDetails(string id, string logId);
        Task<bool> SendDeployStatusUpdate(DeployStatusApiResult updateStatusObj);

        void Delete(string id);
        Task DeployAsync(IRepository repository, ChangeSet changeSet, string deployer, bool clean, DeploymentInfoBase deploymentInfo = null, bool needFileUpdate = true, bool fullBuildByDefault = true);

        /// <summary>
        /// Creates a temporary deployment that is used as a placeholder until changeset details are available.
        /// </summary>
        IDisposable CreateTemporaryDeployment(string statusText, out ChangeSet tempChangeSet, ChangeSet changeset = null, string deployedBy = null);

        ILogger GetLogger(string id);

        ILogger GetLogger(string id, ITracer tracer, DeploymentInfoBase deploymentInfo);

        string GetDeploymentScriptContent();
    }
}
