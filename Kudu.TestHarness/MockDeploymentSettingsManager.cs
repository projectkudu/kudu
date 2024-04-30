using System;
using System.Collections.Generic;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Test
{
    public class MockDeploymentSettingsManager : IDeploymentSettingsManager
    {
        private static Dictionary<string, string> _defaultSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { SettingsKeys.DeploymentBranch, "master" },
            { SettingsKeys.TraceLevel, ((int)DeploymentSettingsExtension.DefaultTraceLevel).ToString() },
            { SettingsKeys.CommandIdleTimeout, ((int)DeploymentSettingsExtension.DefaultCommandIdleTimeout.TotalSeconds).ToString() },
            { SettingsKeys.BuildArgs, "" }
        };

        public Dictionary<string, string> _settings = new Dictionary<string, string>(_defaultSettings);

        public void SetValue(string key, string value)
        {
            _settings[key] = value;
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            return _settings;
        }

        public string GetValue(string key, bool preventUnification = false)
        {
            string value;
            _settings.TryGetValue(key, out value);
            return value;
        }

        public void DeleteValue(string key)
        {
            _settings.Remove(key);
        }

        public IEnumerable<ISettingsProvider> SettingsProviders
        {
            get { return new ISettingsProvider[0]; }
        }

        public string GetHostingConfiguration(string key, string defaultValue)
        {
            return defaultValue;
        }
    }
}
