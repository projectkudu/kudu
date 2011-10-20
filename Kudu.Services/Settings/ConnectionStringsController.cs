using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Deployment;

namespace Kudu.Services.Settings {
    [ServiceContract]
    public class ConnectionStringsController {
        private readonly IDeploymentSettingsManager _settingsManager;
        public ConnectionStringsController(IDeploymentSettingsManager settingsManager) {
            _settingsManager = settingsManager;
        }
        
        [WebGet(UriTemplate = "")]
        public IEnumerable<ConnectionStringSetting> Index() {
            return _settingsManager.GetConnectionStrings();
        }

        [WebInvoke]
        public void Set(SimpleJson.JsonObject input) {
            _settingsManager.SetConnectionString((string)input["key"], (string)input["value"]);
        }

        [WebInvoke]
        public void Remove(SimpleJson.JsonObject input) {
            _settingsManager.RemoveConnectionString((string)input["key"]);
        }
    }
}
