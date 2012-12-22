using System;
using System.Collections.Generic;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Test
{
    public class MockDeploymentSettingsManager : IDeploymentSettingsManager
    {
        private static Dictionary<string, string> _defaultSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { SettingsKeys.Branch, "master" },
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

        public string GetValue(string key)
        {
            return _settings[key];
        }

        public void DeleteValue(string key)
        {
            _settings.Remove(key);
        }
    }
}
