using System.Collections.Generic;
using System.Linq;
using Kudu.Contracts.Settings;
using XmlSettings;

namespace Kudu.Core.Settings
{
    /// <summary>
    /// Settings provider for settings that are updated by the kudu settings api,
    /// These settings are stored in a settings.xml file on the site.
    /// </summary>
    public class PerSiteSettingsProvider : ISettingsProvider
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly ISettings _settings;

        public PerSiteSettingsProvider(ISettings settings)
        {
            _settings = settings;
        }

        public void SetValue(string key, string value)
        {
            // TODO: key/value validator
            _settings.SetValue(DeploymentSettingsSection, key, value);
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            IList<KeyValuePair<string, string>> values = _settings.GetValues(DeploymentSettingsSection);
            if (values != null)
            {
                return values.Select(s => new KeyValuePair<string, string>(s.Key, s.Value));
            }

            return new KeyValuePair<string, string>[0];
        }

        public string GetValue(string key)
        {
            return _settings.GetValue(DeploymentSettingsSection, key);
        }

        public void DeleteValue(string key)
        {
            _settings.DeleteValue(DeploymentSettingsSection, key);
        }

        public int Priority
        {
            get { return (int)SettingsProvidersPriority.PerSite; }
        }
    }
}
