using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Deployment;

namespace Kudu.Services.Deployment {
    [ServiceContract]
    public class DeployController {
        private readonly IDeploymentManager _deploymentManager;

        public DeployController(IDeploymentManager deploymentManager) {
            _deploymentManager = deploymentManager;
        }

        [WebGet(UriTemplate = "id")]
        public string GetActiveDeploymentId() {
            return _deploymentManager.ActiveDeploymentId;
        }
        
        [WebInvoke(UriTemplate = "create")]
        public void Deploy() {
            _deploymentManager.Deploy();
        }
        
        [WebInvoke(UriTemplate = "build")]
        public void CreateBuild(SimpleJson.JsonObject input) {
            _deploymentManager.Build((string)input["id"]);
        }

        [WebGet(UriTemplate = "log")]
        public IEnumerable<DeployResult> GetDeployResults() {
            return _deploymentManager.GetResults();
        }

        [WebGet(UriTemplate = "log?id={id}")]
        public IEnumerable<LogEntry> GetLogEntry(string id) {
            return _deploymentManager.GetLogEntries(id);
        }

        [WebGet(UriTemplate = "details?id={id}")]
        public DeployResult GetResult(string id) {
            return _deploymentManager.GetResult(id);
        }
    }
}
