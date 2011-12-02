using System.Collections.Generic;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentSettingsManager : KuduRemoteClientBase, IDeploymentSettingsManager
    {
        public RemoteDeploymentSettingsManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public IEnumerable<DeploymentSetting> GetAppSettings()
        {
            return _client.GetAsyncJson<IEnumerable<DeploymentSetting>>("appSettings");
        }

        public IEnumerable<ConnectionStringSetting> GetConnectionStrings()
        {
            return _client.GetAsyncJson<IEnumerable<ConnectionStringSetting>>("connectionStrings");
        }

        public void SetConnectionString(string name, string connectionString)
        {
            SetValue("connectionStrings", name, connectionString);
        }

        public void RemoveConnectionString(string key)
        {
            DeleteValue("connectionStrings", key);
        }

        public void RemoveAppSetting(string key)
        {
            DeleteValue("appSettings", key);
        }

        public void SetAppSetting(string key, string value)
        {
            SetValue("appSettings", key, value);
        }

        private void SetValue(string section, string key, string value)
        {
            _client.PostAsync(section + "/set", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("key", key), new KeyValuePair<string, string>("value", value)))
                   .Result
                   .EnsureSuccessful();
        }

        private void DeleteValue(string section, string key)
        {
            _client.PostAsync(section + "/remove", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("key", key)))
                   .Result
                   .EnsureSuccessful();
        }
    }
}