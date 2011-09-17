using System;
using System.Collections.Generic;
using System.Linq;
using XmlSettings;

namespace Kudu.Core.Deployment {
    public class DeploymentSettingsManager : IDeploymentSettingsManager {
        private readonly ISettings _settings;

        public DeploymentSettingsManager(ISettings settings) {
            _settings = settings;
        }

        public IEnumerable<DeploymentSetting> GetAppSettings() {
            return GetSettings("appSettings", pair => new DeploymentSetting {
                Key = pair.Key,
                Value = pair.Value
            });
        }

        public IEnumerable<ConnectionStringSetting> GetConnectionStrings() {
            return GetSettings("connectionStrings", pair => new ConnectionStringSetting {
                Name = pair.Key,
                ConnectionString = pair.Value
            });
        }

        private IEnumerable<T> GetSettings<T>(string sectionName, Func<KeyValuePair<string, string>, T> selector) {
            var section = _settings.GetValues(sectionName);

            if (section != null) {
                return section.Select(selector);
            }

            return Enumerable.Empty<T>();
        }

        public void SetConnectionString(string key, string value) {
            _settings.SetValue("connectionStrings", key, value);
        }

        public void RemoveConnectionString(string key) {
            _settings.DeleteValue("connectionStrings", key);
        }

        public void RemoveAppSetting(string key) {
            _settings.DeleteValue("appSettings", key);
        }

        public void SetAppSetting(string key, string value) {
            _settings.SetValue("appSettings", key, value);
        }
    }
}
