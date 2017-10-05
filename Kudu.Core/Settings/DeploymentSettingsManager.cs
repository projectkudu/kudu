using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Contracts.Settings;
using XmlSettings;

namespace Kudu.Core.Settings
{
    public class DeploymentSettingsManager : IDeploymentSettingsManager
    {
        private readonly PerSiteSettingsProvider _perSiteSettings;
        private readonly List<ISettingsProvider> _settingsProviders;

        public DeploymentSettingsManager(ISettings settings)
            : this(settings, new EnvironmentSettingsProvider(), new DefaultSettingsProvider())
        {
        }

        internal DeploymentSettingsManager(ISettings settings, params ISettingsProvider[] settingsProviders)
            : this(new PerSiteSettingsProvider(settings), settingsProviders)
        {
        }

        internal DeploymentSettingsManager(PerSiteSettingsProvider perSiteSettings, params ISettingsProvider[] settingsProviders)
        {
            var settingsProvidersList = new List<ISettingsProvider>();

            if (perSiteSettings != null)
            {
                _perSiteSettings = perSiteSettings;
                settingsProvidersList.Add(_perSiteSettings);
            }

            settingsProvidersList.AddRange(settingsProviders);

            _settingsProviders = settingsProvidersList.OrderByDescending(s => s.Priority).ToList();
        }

        public IEnumerable<ISettingsProvider> SettingsProviders
        {
            get { return _settingsProviders; }
        }

        public static IDeploymentSettingsManager BuildPerDeploymentSettingsManager(string path, IEnumerable<ISettingsProvider> settingsProviders)
        {
            var combinedSettingsProviders = new List<ISettingsProvider>(settingsProviders);
            combinedSettingsProviders.Add(new DeploymentSettingsProvider(path));

            PerSiteSettingsProvider perSiteSettings = null;
            return new DeploymentSettingsManager(perSiteSettings, combinedSettingsProviders.ToArray());
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var reverseProviders = _settingsProviders.Reverse<ISettingsProvider>();
            foreach (var provider in reverseProviders)
            {
                foreach (var keyValuePair in provider.GetValues())
                {
                    values[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            return values;
        }

        public string GetValue(string key, bool preventUnification)
        {
            if (preventUnification)
            {
                return _settingsProviders[0].GetValue(key);
            }

            foreach (var provider in _settingsProviders)
            {
                var value = provider.GetValue(key);
                if (!String.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }

        public void SetValue(string key, string value)
        {
            // Note that this only applies to persisted per-site settings
            if (_perSiteSettings != null)
            {
                _perSiteSettings.SetValue(key, value);
            }
        }

        public void DeleteValue(string key)
        {
            // Note that this only applies to persisted per-site settings
            if (_perSiteSettings != null)
            {
                _perSiteSettings.DeleteValue(key);
            }
        }
    }
}
