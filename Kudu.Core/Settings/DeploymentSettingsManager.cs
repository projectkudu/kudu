using System.Collections.Generic;
using System.Linq;
using Kudu.Contracts.Settings;
using XmlSettings;

namespace Kudu.Core.Settings
{
    public class DeploymentSettingsManager : IDeploymentSettingsManager
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly ISettings _settings;

        public DeploymentSettingsManager(ISettings settings)
        {
            _settings = settings;
        }

        public void SetValue(string key, string value)
        {
            _settings.SetValue(DeploymentSettingsSection, key, value);
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            var values = _settings.GetValues(DeploymentSettingsSection);

            if (values == null)
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            return values;
        }

        public string GetValue(string key)
        {
            return _settings.GetValue(DeploymentSettingsSection, key);
        }

        public void DeleteValue(string key)
        {
            _settings.DeleteValue(DeploymentSettingsSection, key);
        }
    }
}
