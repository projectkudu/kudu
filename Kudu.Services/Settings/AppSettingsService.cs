using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Deployment;

namespace Kudu.Services.Settings
{
    [ServiceContract]
    public class AppSettingsService
    {
        private readonly IDeploymentSettingsManager _settingsManager;
        public AppSettingsService(IDeploymentSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        [WebGet(UriTemplate = "")]
        public IEnumerable<DeploymentSetting> Index()
        {
            return _settingsManager.GetAppSettings();
        }

        [WebInvoke]
        public void Set(SimpleJson.JsonObject input)
        {
            _settingsManager.SetAppSetting((string)input["key"], (string)input["value"]);
        }

        [WebInvoke]
        public void Remove(SimpleJson.JsonObject input)
        {
            _settingsManager.RemoveAppSetting((string)input["key"]);
        }
    }
}
