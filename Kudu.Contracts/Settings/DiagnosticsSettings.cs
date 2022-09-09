using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;

namespace Kudu.Contracts.Settings
{
    /// <summary>
    /// This class hold the diagnostics settings which consists of 6 required properties (with default values)
    /// and can also hold other arbitrary settings.
    /// </summary>
    public class DiagnosticsSettings
    {
        public const string AzureDriveEnabledKey = "AzureDriveEnabled";
        public const string AzureDriveTraceLevelKey = "AzureDriveTraceLevel";
        public const string AzureTableEnabledKey = "AzureTableEnabled";
        public const string AzureTableTraceLevelKey = "AzureTableTraceLevel";
        public const string AzureBlobEnabledKey = "AzureBlobEnabled";
        public const string AzureBlobTraceLevelKey = "AzureBlobTraceLevel";
        
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

        public bool AzureDriveEnabled
        {
            get { return (bool)_settings[AzureDriveEnabledKey]; }
            set { _settings[AzureDriveEnabledKey] = value; }
        }

        [JsonInclude]
        [JsonConverter(typeof(StrictStringEnumConverter))]
        public TraceEventType AzureDriveTraceLevel
        {
            get { return (TraceEventType)_settings[AzureDriveTraceLevelKey]; }
            set { _settings[AzureDriveTraceLevelKey] = value; }
        }

        [JsonInclude]
        public bool AzureTableEnabled
        {
            get { return (bool)_settings[AzureTableEnabledKey]; }
            set { _settings[AzureTableEnabledKey] = value; }
        }

        [JsonInclude]
        [JsonConverter(typeof(StrictStringEnumConverter))]
        public TraceEventType AzureTableTraceLevel
        {
            get { return (TraceEventType)_settings[AzureTableTraceLevelKey]; }
            set { _settings[AzureTableTraceLevelKey] = value; }
        }

        [JsonInclude]
        public bool AzureBlobEnabled
        {
            get { return (bool)_settings[AzureBlobEnabledKey]; }
            set { _settings[AzureBlobEnabledKey] = value; }
        }

        [JsonInclude]
        [JsonConverter(typeof(StrictStringEnumConverter))]
        public TraceEventType AzureBlobTraceLevel
        {
            get { return (TraceEventType)_settings[AzureBlobTraceLevelKey]; }
            set { _settings[AzureBlobTraceLevelKey] = value; }
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
        private class StrictStringEnumConverter : JsonConverter<TraceEventType>
        {

            public override TraceEventType Read(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
            {
                int unused;
                if (reader.TokenType != JsonTokenType.Number)
                {
                    try
                    {
                        return (TraceEventType)Enum.Parse(objectType, reader.GetString());
                    }
                    catch
                    {
                        throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Error converting value '{0}' from type '{1}'", reader.ValueSequence, reader.TokenType));
                    }
                }

                throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Error converting value '{0}' from type '{1}'", reader.ValueSequence, reader.TokenType));
            }

            public override void Write(Utf8JsonWriter writer, TraceEventType objectType, JsonSerializerOptions options)
            {
                if (Enum.IsDefined(objectType.GetType(), objectType))
                {
                    writer.WriteStringValue(objectType.ToString());
                    return;
                }

                throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Error converting value '{0}'", objectType));
            }
        }
    }
}
