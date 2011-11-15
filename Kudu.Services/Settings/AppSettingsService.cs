using System.Collections.Generic;
using System.ComponentModel;
using System.Json;
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

        [Description("Gets the current set of environment variables.")]
        [WebGet(UriTemplate = "")]
        public IEnumerable<DeploymentSetting> Index()
        {
            return _settingsManager.GetAppSettings();
        }

        [Description("Creates or sets an environment variable.")]
        [WebInvoke(UriTemplate = "set")]
        public void Set(JsonObject input)
        {
            _settingsManager.SetAppSetting((string)input["key"], (string)input["value"]);
        }

        [Description("Removes an environment variable.")]
        [WebInvoke(UriTemplate = "remove")]
        public void Remove(JsonObject input)
        {
            _settingsManager.RemoveAppSetting((string)input["key"]);
        }
    }
}
