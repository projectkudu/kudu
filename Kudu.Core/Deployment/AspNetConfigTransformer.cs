using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Deployment {
    public class AspNetConfigTransformer {
        private readonly IDeploymentSettingsManager _settingsManager;
        private readonly IFileSystem _fileSystem;

        public AspNetConfigTransformer(IFileSystem fileSystem,
                                       IDeploymentSettingsManager settingsManager) {
            _fileSystem = fileSystem;
            _settingsManager = settingsManager;
        }

        public void PerformTransformations(string path) {
            // Only transform configuration files in the root
            string targetConfig = Path.Combine(path, "web.config");

            // Get the fileinfo for the web config
            FileInfoBase fileInfo = _fileSystem.FileInfo.FromFileName(targetConfig);

            // If there's no web.config at the root then do nothing
            if (!fileInfo.Exists) {
                return;
            }

            // Get the config content
            string content = null;
            using (Stream stream = fileInfo.OpenRead()) {
                using (var reader = new StreamReader(stream)) {
                    content = reader.ReadToEnd();
                }
            }

            // Transfor the configuration and overwrite the config file
            using (Stream stream = fileInfo.Create()) {
                XDocument document = Transform(content);
                document.Save(stream);
            }
        }

        internal XDocument Transform(string content) {
            var configuration = XDocument.Parse(content);

            // Transform the app settings if there's any
            ProcessAppSettings(configuration);

            ProcessConnectionStrings(configuration);

            return configuration;
        }

        private void ProcessConnectionStrings(XDocument configuration) {
            IEnumerable<ConnectionStringSetting> connectionStrings = _settingsManager.GetConnectionStrings();
            if (!connectionStrings.Any()) {
                return;
            }

            // Add the connection string settings element if needed
            XElement connectionStringsElement = GetElement(configuration.Root, "connectionStrings", createIfNotExists: false);

            // Do nothing if there are no connection strings to replace.
            if (connectionStringsElement == null) {
                return;
            }

            IDictionary<string, XElement> connectionStringEntries = GetDictionary(connectionStringsElement, "name");

            foreach (var connectionString in connectionStrings) {
                XElement connectionStringEntry;
                if (!connectionStringEntries.TryGetValue(connectionString.Name, out connectionStringEntry)) {
                    // Only replace connectionstrings, don't add new ones
                    continue;
                }

                connectionStringEntry.SetAttributeValue("name", connectionString.Name);
                connectionStringEntry.SetAttributeValue("connectionString", connectionString.ConnectionString);
                if (!String.IsNullOrEmpty(connectionString.ProviderName)) {
                    connectionStringEntry.SetAttributeValue("providerName", connectionString.ProviderName);
                }
            }
        }

        private void ProcessAppSettings(XDocument configuration) {
            IEnumerable<DeploymentSetting> appSettings = _settingsManager.GetAppSettings();

            if (!appSettings.Any()) {
                return;
            }

            XElement appSettingsElement = GetElement(configuration.Root, "appSettings");
            IDictionary<string, XElement> appSettingsEntries = GetDictionary(appSettingsElement, "key");

            foreach (var setting in appSettings) {
                XElement appSettingEntry;
                if (!appSettingsEntries.TryGetValue(setting.Key, out appSettingEntry)) {
                    appSettingEntry = new XElement("add");
                    appSettingsElement.Add(appSettingEntry);
                }

                appSettingEntry.SetAttributeValue("key", setting.Key);
                appSettingEntry.SetAttributeValue("value", setting.Value);
            }
        }

        private IDictionary<string, XElement> GetDictionary(XElement element, string attributeName) {
            var elements = from e in element.Elements()
                           let keyAttr = e.Attribute(attributeName)
                           where keyAttr != null
                           select new {
                               Key = keyAttr.Value,
                               Element = e
                           };

            return elements.ToDictionary(e => e.Key, e => e.Element, StringComparer.OrdinalIgnoreCase);
        }

        private static XElement GetElement(XElement element, string name, bool createIfNotExists = true) {
            var childElement = element.Element(name);
            if (childElement == null && createIfNotExists) {
                childElement = new XElement(name);
                element.Add(childElement);
            }
            return childElement;
        }
    }
}
