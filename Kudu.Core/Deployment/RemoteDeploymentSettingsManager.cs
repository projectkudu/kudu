using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
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

        public void SetConnectionString(string key, string value) {
            SetValue("connectionStrings", key, value);
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
            _client.Post(section + "/set", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "key", key },
                { "value", value }
            })).EnsureSuccessful();
        }

        private void DeleteValue(string section, string key) {
            _client.Post(section + "/remove", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "key", key }
            })).EnsureSuccessful();
        }
    }
}