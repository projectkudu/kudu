using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Kudu.Contracts.Settings
{
    /// <summary>
    /// This class hold the diagnostics settings which consists of 6 required properties (with default values)
    /// and can also hold other arbitrary settings.
    /// </summary>
    public class DiagnosticsSettings
    {
        // Holds the required settings
        private readonly Dictionary<string, object> _settings = new Dictionary<string, object>();

        // Holds the dynamic optional settings
        [JsonExtensionData]
        private readonly Dictionary<string, object> _extraSettings = new Dictionary<string, object>();

        public DiagnosticsSettings()
        {
            // Set default values for required settings
            AzureDriveEnabled = false;
            AzureDriveTraceLevel = TraceEventType.Error;
            AzureTableEnabled = false;
            AzureTableTraceLevel = TraceEventType.Error;
            AzureBlobEnabled = false;
            AzureBlobTraceLevel = TraceEventType.Error;
        }

        [JsonProperty]
        public bool AzureDriveEnabled
        {
            get { return (bool)_settings["AzureDriveEnabled"]; }
            set { _settings["AzureDriveEnabled"] = value; }
        }

        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public TraceEventType AzureDriveTraceLevel
        {
            get { return (TraceEventType)_settings["AzureDriveTraceLevel"]; }
            set { _settings["AzureDriveTraceLevel"] = value; }
        }

        [JsonProperty]
        public bool AzureTableEnabled
        {
            get { return (bool)_settings["AzureTableEnabled"]; }
            set { _settings["AzureTableEnabled"] = value; }
        }

        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public TraceEventType AzureTableTraceLevel
        {
            get { return (TraceEventType)_settings["AzureTableTraceLevel"]; }
            set { _settings["AzureTableTraceLevel"] = value; }
        }

        [JsonProperty]
        public bool AzureBlobEnabled
        {
            get { return (bool)_settings["AzureBlobEnabled"]; }
            set { _settings["AzureBlobEnabled"] = value; }
        }

        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public TraceEventType AzureBlobTraceLevel
        {
            get { return (TraceEventType)_settings["AzureBlobTraceLevel"]; }
            set { _settings["AzureBlobTraceLevel"] = value; }
        }

        public object GetSetting(string key)
        {
            object value;

            if (_settings.TryGetValue(key, out value))
            {
                return value;
            }

            if (_extraSettings.TryGetValue(key, out value))
            {
                return value;
            }

            return null;
        }

        public void SetSetting(string key, object value)
        {
            if (_settings.ContainsKey(key))
            {
                _settings[key] = value;
            }
            else
            {
                _extraSettings[key] = value;
            }
        }

        public bool RemoveSetting(string key)
        {
            return _extraSettings.Remove(key);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _settings.Union(_extraSettings).GetEnumerator();
        }
    }
}
