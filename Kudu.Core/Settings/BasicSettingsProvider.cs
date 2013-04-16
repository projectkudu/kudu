using System;
using System.Collections.Generic;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Settings
{
    public class BasicSettingsProvider : ISettingsProvider
    {
        private readonly SettingsProvidersPriority _priority;
        private readonly Dictionary<string, string> _settings;

        public BasicSettingsProvider(IDictionary<string, string> settings, SettingsProvidersPriority priority)
        {
            _priority = priority;
            _settings = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            return _settings;
        }

        public string GetValue(string key)
        {
            string value;
            _settings.TryGetValue(key, out value);
            return value;
        }

        public int Priority
        {
            get { return (int)_priority; }
        }
    }
}
