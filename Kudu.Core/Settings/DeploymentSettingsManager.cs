using System;
using System.Collections.Generic;
using System.Configuration;
using Kudu.Contracts.Settings;
using XmlSettings;

namespace Kudu.Core.Settings
{
    public class DeploymentSettingsManager : IDeploymentSettingsManager
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly ISettings _perSiteSettings;

        // Ideally, these default settings would live in Kudu's web.config. However, we also need them in 
        // kudu.exe, so they actually need to be in a shared config file. For now, it's easier to hard code
        // the defaults, since things like 'branch' will rarely want a different global default
        private static Dictionary<string, string> _defaultSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { SettingsKeys.Branch, "master" },
            { SettingsKeys.TraceLevel, ((int)DeploymentSettingsExtension.DefaultTraceLevel).ToString() },
            { SettingsKeys.CommandIdleTimeout, ((int)DeploymentSettingsExtension.DefaultCommandIdleTimeout.TotalSeconds).ToString() },
            { SettingsKeys.BuildArgs, "" }
        };

        static DeploymentSettingsManager()
        {
            // Add all the .net <appSettings> (which themselves include portal settings in Azure!).
            foreach (string name in ConfigurationManager.AppSettings)
            {
                _defaultSettings[name] = ConfigurationManager.AppSettings[name];
            }
        }

        public DeploymentSettingsManager(ISettings settings)
        {
            _perSiteSettings = settings;
        }

        public void SetValue(string key, string value)
        {
            // TODO: key/value validator

            // Note that this only applies to persisted per-site settings
            _perSiteSettings.SetValue(DeploymentSettingsSection, key, value);
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            // Start with the default values
            var values = new Dictionary<string, string>(_defaultSettings, StringComparer.OrdinalIgnoreCase);

            // Add all the per-site settings, overriding current values if needed
            var settings = _perSiteSettings.GetValues(DeploymentSettingsSection);
            if (settings != null)
            {
                foreach (var entry in settings)
                {
                    values[entry.Key] = entry.Value;
                }
            }

            return values;
        }

        public string GetValue(string key)
        {
            // First try the per-site persisted settings
            string val = _perSiteSettings.GetValue(DeploymentSettingsSection, key);
            if (!String.IsNullOrEmpty(val))
            {
                return val;
            }

            // Fall back to the defaults (which include .NET appsettings)
            _defaultSettings.TryGetValue(key, out val);

            return val;
        }

        public void DeleteValue(string key)
        {
            // Note that this only applies to persisted per-site settings
            _perSiteSettings.DeleteValue(DeploymentSettingsSection, key);
        }
    }
}
