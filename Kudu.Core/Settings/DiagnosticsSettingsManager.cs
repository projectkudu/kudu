using System;
using System.Collections.Generic;
using System.IO;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Core.Settings
{
    public class DiagnosticsSettingsManager
    {
        private readonly string _path;
        private readonly ITracer _tracer;

        public DiagnosticsSettingsManager(string path, ITracer tracer)
        {
            _path = path;
            _tracer = tracer;
        }

        public object GetSetting(string key)
        {
            DiagnosticsSettings settings = ReadSettings();
            return settings.GetSetting(key);
        }

        public DiagnosticsSettings GetSettings()
        {
            return ReadSettings();
        }

        public void UpdateSetting(string key, object value)
        {
            DiagnosticsSettings diagnosticsSettings = ReadSettings();
            diagnosticsSettings.SetSetting(key, value);
            SaveSettings(diagnosticsSettings);
        }

        public void UpdateSettings(DiagnosticsSettings settings)
        {
            DiagnosticsSettings diagnosticsSettings = ReadSettings();
            foreach (KeyValuePair<string, object> pair in settings)
            {
                diagnosticsSettings.SetSetting(pair.Key, pair.Value);
            }

            SaveSettings(diagnosticsSettings);
        }

        public bool DeleteSetting(string key)
        {
            DiagnosticsSettings diagnosticsSettings = ReadSettings();
            if (diagnosticsSettings.RemoveSetting(key))
            {
                SaveSettings(diagnosticsSettings);
                return true;
            }

            return false;
        }

        private DiagnosticsSettings ReadSettings()
        {
            if (FileSystemHelpers.FileExists(_path))
            {
                try
                {
                    string fileContent = null;
                    OperationManager.Attempt(() => fileContent = FileSystemHelpers.ReadAllTextFromFile(_path));

                    var settings = JsonConvert.DeserializeObject<DiagnosticsSettings>(fileContent);
                    if (settings == null)
                    {
                        throw new InvalidOperationException($"File '{_path}' content is empty or contains only white spaces.");
                    }

                    return settings;
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex);

                    // there must be corrupted value, delete file to reset everything to default
                    FileSystemHelpers.DeleteFileSafe(_path);
                }
            }

            return new DiagnosticsSettings();
        }

        private void SaveSettings(DiagnosticsSettings diagnosticsSettings)
        {
            if (!FileSystemHelpers.FileExists(_path))
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_path));
            }

            string fileContent = JsonConvert.SerializeObject(diagnosticsSettings);
            FileSystemHelpers.WriteAllTextToFile(_path, fileContent);
        }
    }
}