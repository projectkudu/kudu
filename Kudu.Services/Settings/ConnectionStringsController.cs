using System.Collections.Generic;
using Kudu.Core.Deployment;
using Kudu.Services.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Kudu.Services.Settings {
    [ServiceContract]
    public class ConnectionStringsController {
        private readonly IDeploymentSettingsManager _settingsManager;
        public ConnectionStringsController(IDeploymentSettingsManager _settingsManager) {
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
