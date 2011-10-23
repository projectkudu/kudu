using System.Collections.Generic;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;

namespace Kudu.Client.Deployment {
    public class RemoteDeploymentSettingsManager : IDeploymentSettingsManager {
        private readonly HttpClient _client;

        public RemoteDeploymentSettingsManager(string serviceUrl) {
            serviceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public IEnumerable<DeploymentSetting> GetAppSettings() {
            return _client.GetJson<IEnumerable<DeploymentSetting>>("appSettings");
        }

        public IEnumerable<ConnectionStringSetting> GetConnectionStrings() {
            return _client.GetJson<IEnumerable<ConnectionStringSetting>>("connectionStrings");
        }

        public void SetConnectionString(string name, string connectionString) {
            SetValue("connectionStrings", name, connectionString);
        }

        public void RemoveConnectionString(string key) {
            DeleteValue("connectionStrings", key);
        }

        public void RemoveAppSetting(string key) {
            DeleteValue("appSettings", key);
        }

        public void SetAppSetting(string key, string value) {
            SetValue("appSettings", key, value);
        }

        private void SetValue(string section, string key, string value) {
            _client.Post(section + "/set", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("key", key), new KeyValuePair<string, object>("value", value)))
                   .EnsureSuccessful();
        }

        private void DeleteValue(string section, string key) {
            _client.Post(section + "/remove", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("key", key)))
                   .EnsureSuccessful();
        }
    }
}