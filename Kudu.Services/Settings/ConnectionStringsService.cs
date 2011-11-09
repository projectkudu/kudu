using System.Collections.Generic;
using System.Json;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Deployment;

namespace Kudu.Services.Settings
{
    [ServiceContract]
    public class ConnectionStringsService
    {
        private readonly IDeploymentSettingsManager _settingsManager;
        public ConnectionStringsService(IDeploymentSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        [WebGet(UriTemplate = "")]
        public IEnumerable<ConnectionStringSetting> Index()
        {
            return _settingsManager.GetConnectionStrings();
        }

        [WebInvoke]
        public void Set(JsonObject input)
        {
            _settingsManager.SetConnectionString((string)input["key"], (string)input["value"]);
        }

        [WebInvoke]
        public void Remove(JsonObject input)
        {
            _settingsManager.RemoveConnectionString((string)input["key"]);
        }
    }
}
