using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
    public class RemoteDeploymentSettingsProvider : IDeploymentSettingsProvider {
        private readonly HttpClient _client;

        public RemoteDeploymentSettingsProvider(string serviceUrl) {
            serviceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public IEnumerable<DeploymentSetting> GetAppSettings() {
            return _client.GetJson<IEnumerable<DeploymentSetting>>("appSettings");
        }

        public IEnumerable<DeploymentSetting> GetConnectionStrings() {
            return _client.GetJson<IEnumerable<DeploymentSetting>>("connectionStrings");
        }
    }
}
