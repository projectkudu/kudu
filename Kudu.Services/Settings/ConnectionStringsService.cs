using System.Collections.Generic;
using System.ComponentModel;
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

        [Description("Gets the current set of connection strings.")]
        [WebGet(UriTemplate = "")]
        public IEnumerable<ConnectionStringSetting> Index()
        {
            return _settingsManager.GetConnectionStrings();
        }

        [Description("Creates or sets a connection string.")]
        [WebInvoke(UriTemplate = "set")]
        public void Set(JsonObject input)
        {
            _settingsManager.SetConnectionString((string)input["key"], (string)input["value"]);
        }

        [Description("Removes a connection string.")]
        [WebInvoke(UriTemplate = "remove")]
        public void Remove(JsonObject input)
        {
            _settingsManager.RemoveConnectionString((string)input["key"]);
        }
    }
}
