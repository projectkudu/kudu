using System.Collections.Generic;
using System.ComponentModel;
using System.Json;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Deployment;

namespace Kudu.Services.Deployment
{
    [ServiceContract]
    public class DeploymentService
    {
        private readonly IDeploymentManager _deploymentManager;

        public DeploymentService(IDeploymentManager deploymentManager)
        {
            _deploymentManager = deploymentManager;
        }

        [Description("Gets the id of the current active deployment.")]
        [WebGet(UriTemplate = "id")]
        public string GetActiveDeploymentId()
        {
            return _deploymentManager.ActiveDeploymentId;
        }

        [Description("Deletes a deployment.")]
        [WebInvoke(UriTemplate = "delete")]
        public void Delete(JsonObject input)
        {
            _deploymentManager.Delete((string)input["id"]);
        }

        [Description("Performs a deployment.")]
        [WebInvoke(UriTemplate = "")]
        public void Deploy()
        {
            _deploymentManager.Deploy();
        }

        [Description("Restore a previously successful deployment based on its id.")]
        [WebInvoke(UriTemplate = "restore")]
        public void Restore(JsonObject input)
        {
            _deploymentManager.Deploy((string)input["id"]);
        }

        [Description("Builds a specific deployment based on its id.")]
        [WebInvoke(UriTemplate = "build")]
        public void Build(JsonObject input)
        {
            _deploymentManager.Build((string)input["id"]);
        }

        [Description("Gets the deployment results of all deployments.")]
        [WebGet(UriTemplate = "log")]
        public IEnumerable<DeployResult> GetDeployResults()
        {
            return _deploymentManager.GetResults();
        }

        [Description("Gets the log of a specific deployment based on its id.")]
        [WebGet(UriTemplate = "log?id={id}")]
        public IEnumerable<LogEntry> GetLogEntry(string id)
        {
            return _deploymentManager.GetLogEntries(id);
        }

        [Description("Gets the deployment result of a specific deployment based on its id.")]
        [WebGet(UriTemplate = "details?id={id}")]
        public DeployResult GetResult(string id)
        {
            return _deploymentManager.GetResult(id);
        }
    }
}
