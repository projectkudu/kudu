using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using Kudu.Contracts.Settings;
using XmlSettings;

namespace Kudu.Core.Settings
{
    public class DeploymentSettingsManager : IDeploymentSettingsManager
    {
        private static readonly Lazy<IDictionary<string, string>> _defaultSettingsFactory = new Lazy<IDictionary<string, string>>(GetDefaultSettingsInternal);
        private const string DeploymentSettingsSection = "deployment";
        private const string AppSettingPrefix = "APPSETTING_";
        private readonly ISettings _perSiteSettings;
        private readonly IDictionary<string, string> _defaultSettings;

        public DeploymentSettingsManager(ISettings settings) 
            : this(settings, _defaultSettingsFactory.Value)
        {
            
        }

        public DeploymentSettingsManager(ISettings settings, IDictionary<string, string> defaultSettings)
        {
            _perSiteSettings = settings;
            _defaultSettings = defaultSettings;
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

        public string GetValue(string key, bool preventUnification)
        {
            // First try the per-site persisted settings
            string val = _perSiteSettings.GetValue(DeploymentSettingsSection, key);
            if (!String.IsNullOrEmpty(val))
            {
                return val;
            }

            // Fall back to the defaults (which include .NET appsettings)
            if (!preventUnification)
            {
                _defaultSettings.TryGetValue(key, out val);
            }

            return val;
        }

        public void DeleteValue(string key)
        {
            // Note that this only applies to persisted per-site settings
            _perSiteSettings.DeleteValue(DeploymentSettingsSection, key);
        }

        private static IDictionary<string, string> GetDefaultSettingsInternal()
        {
            // Ideally, these default settings would live in Kudu's web.config. However, we also need them in 
            // kudu.exe, so they actually need to be in a shared config file. For now, it's easier to hard code
            // the defaults, since things like 'branch' will rarely want a different global default

            var defaultSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { SettingsKeys.DeploymentBranch, "master" },
                { SettingsKeys.TraceLevel, ((int)DeploymentSettingsExtension.DefaultTraceLevel).ToString() },
                { SettingsKeys.CommandIdleTimeout, ((int)DeploymentSettingsExtension.DefaultCommandIdleTimeout.TotalSeconds).ToString() },
                { SettingsKeys.LogStreamTimeout, ((int)DeploymentSettingsExtension.DefaultLogStreamTimeout.TotalSeconds).ToString() },
                { SettingsKeys.BuildArgs, "" }
            };

            // Add all the .net <appSettings> (which themselves include portal settings in Azure!).
            foreach (string name in ConfigurationManager.AppSettings)
            {
                defaultSettings[name] = ConfigurationManager.AppSettings[name];
            }

            // Go through all the environment variables and process those that are meant to be app settings.
            // In Azure, those will already have been present in ConfigurationManager.AppSettings when running
            // in the Kudu service. But when running in kudu.exe, they wouldn't (hence the need for this code)
            foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                var name = (string)entry.Key;

                if (name.StartsWith(AppSettingPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(AppSettingPrefix.Length);
                    defaultSettings[name] = (string)entry.Value;
                }
            }

            return defaultSettings;
        }
    }
}
