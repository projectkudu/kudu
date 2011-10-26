using System.Collections.Generic;
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

        [WebGet(UriTemplate = "id")]
        public string GetActiveDeploymentId()
        {
            return _deploymentManager.ActiveDeploymentId;
        }

        [WebInvoke(UriTemplate = "")]
        public void Deploy()
        {
            _deploymentManager.Deploy();
        }

        [WebInvoke(UriTemplate = "restore")]
        public void Restore(SimpleJson.JsonObject input)
        {
            _deploymentManager.Deploy((string)input["id"]);
        }

        [WebInvoke(UriTemplate = "build")]
        public void Build(SimpleJson.JsonObject input)
        {
            _deploymentManager.Build((string)input["id"]);
        }

        [WebGet(UriTemplate = "log")]
        public IEnumerable<DeployResult> GetDeployResults()
        {
            return _deploymentManager.GetResults();
        }

        [WebGet(UriTemplate = "log?id={id}")]
        public IEnumerable<LogEntry> GetLogEntry(string id)
        {
            return _deploymentManager.GetLogEntries(id);
        }

        [WebGet(UriTemplate = "details?id={id}")]
        public DeployResult GetResult(string id)
        {
            return _deploymentManager.GetResult(id);
        }
    }
}
