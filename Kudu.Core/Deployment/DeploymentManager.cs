using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager {
        private readonly IRepositoryManager _repositoryManager;
        private readonly ISiteBuilderFactory _builderFactory;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settingsProvider;

        public event Action<DeployResult> StatusChanged;

        public DeploymentManager(IRepositoryManager repositoryManager,
                                 ISiteBuilderFactory builderFactory,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settingsProvider) {
            _repositoryManager = repositoryManager;
            _builderFactory = builderFactory;
            _environment = environment;
            _settingsProvider = settingsProvider;
        }

        public string ActiveDeploymentId {
            get {
                string path = GetActiveDeploymentFilePath();
                if (File.Exists(path)) {
                    return File.ReadAllText(path);
                }
                return null;
            }
        }

        public IEnumerable<DeployResult> GetResults() {
            if (!Directory.Exists(_environment.DeploymentCachePath)) {
                yield break;
            }

            foreach (var id in Directory.EnumerateDirectories(_environment.DeploymentCachePath)) {
                var result = GetResult(id);
                if (result != null) {
                    yield return result;
                }
            }
        }

        public DeployResult GetResult(string id) {
            var file = OpenTrackingFile(id);

            if (file == null) {
                return null;
            }

            return new DeployResult {
                Id = file.Id,
                DeployStartTime = file.DeploymentStartTime,
                DeployEndTime = file.DeploymentEndTime,
                Status = file.Status,
                Percentage = file.Percentage,
                StatusText = file.StatusText
            };
        }

        public IEnumerable<LogEntry> GetLogEntries(string id) {
            string path = GetLogPath(id);

            if (!File.Exists(path)) {
                throw new InvalidOperationException(String.Format("No log found for '{0}'.", id));
            }

            return new XmlLogger(path).GetLogEntries();
        }

        public void Deploy(string id) {
            string cachePath = GetCachePath(id);

            if (!Directory.Exists(cachePath)) {
                throw new InvalidOperationException(String.Format("Unable to deploy '{0}'. No deployments found.", id));
            }

            // We don't want to skip old files here since we might be going back in time
            DeployToTarget(id, skipOldFiles: false);
        }

        public void Deploy() {
            var repository = _repositoryManager.GetRepository();

            if (repository == null) {
                return;
            }

            string id = repository.CurrentId;

            if (String.IsNullOrEmpty(id)) {
                id = repository.GetChanges(0, 1).Single().Id;
                repository.Update(id);
            }

            Branch activeBranch = (from b in repository.GetBranches()
                                   let change = repository.GetDetails(b.Id)
                                   orderby change.ChangeSet.Timestamp descending
                                   select b).FirstOrDefault();

            if (activeBranch != null) {
                repository.Update(activeBranch.Name);
                id = activeBranch.Id;
            }
            else {
                repository.Update(id);
            }

            Build(id);
        }

        public void Build(string id) {
            if (String.IsNullOrEmpty(id)) {
                throw new ArgumentException();
            }

            // TODO: Make sure if the if is a valid changeset            
            ILogger logger = null;
            DeploymentStatusFile trackingFile = null;

            try {
                // Get the logger for this id
                string logPath = GetLogPath(id);
                FileSystemHelpers.DeleteFileSafe(logPath);
                NotifyStatus(id);

                logger = GetLogger(id);

                logger.Log("Preparing deployment for {0}.", id);

                // Put bits in the cache folder
                string cachePath = GetCachePath(id);

                // The initial status
                trackingFile = CreateTrackingFile(id);
                trackingFile.Id = id;
                trackingFile.Status = DeployStatus.Building;
                trackingFile.StatusText = String.Format("Building {0}...", id);
                trackingFile.Save();

                NotifyStatus(id);

                // Create a deployer
                ISiteBuilder builder = _builderFactory.CreateBuilder();

                builder.Build(cachePath, logger)
                       .ContinueWith(t => {
                           if (t.IsFaulted) {
                               // We need to read the exception so the process doesn't go down
                               Exception exception = t.Exception;

                               logger.Log("Deployment failed.", LogEntryType.Error);

                               // failed to deploy
                               trackingFile.Percentage = 100;
                               trackingFile.Status = DeployStatus.Failed;
                               trackingFile.StatusText = String.Empty;
                               trackingFile.DeploymentEndTime = DateTime.Now;
                               trackingFile.Save();

                               NotifyStatus(id);
                           }
                           else {
                               trackingFile.Percentage = 50;
                               trackingFile.Save();
                               NotifyStatus(id);

                               DeployToTarget(id);
                           }
                       });
            }
            catch (Exception e) {
                if (logger != null) {
                    logger.Log("Deployment failed.", LogEntryType.Error);
                    logger.Log(e);
                    NotifyStatus(id);
                }
            }
        }

        private void DeployToTarget(string id, bool skipOldFiles = true) {
            DeploymentStatusFile trackingFile = null;
            ILogger logger = null;

            try {
                string cachePath = GetCachePath(id);
                trackingFile = OpenTrackingFile(id);
                logger = GetLogger(id);

                trackingFile.Status = DeployStatus.Deploying;
                trackingFile.StatusText = "Deploying to webroot...";
                trackingFile.Save();
                NotifyStatus(id);

                logger.Log("Copying files to {0}.", _environment.DeploymentTargetPath);

                // Copy to target
                FileSystemHelpers.SmartCopy(cachePath, _environment.DeploymentTargetPath, file => ApplySettingsTransformation(cachePath, logger, file), skipOldFiles);

                trackingFile.Status = DeployStatus.Success;
                trackingFile.StatusText = String.Empty;

                // Write the active deployment file
                string activeFilePath = GetActiveDeploymentFilePath();
                File.WriteAllText(activeFilePath, id);

                logger.Log("Deployment successful.");
            }
            catch (Exception e) {
                if (trackingFile != null) {
                    trackingFile.Status = DeployStatus.Failed;
                }

                if (logger != null) {
                    logger.Log("Deploying to web root failed.", LogEntryType.Error);
                    logger.Log(e);
                }
            }
            finally {
                if (trackingFile != null) {
                    trackingFile.DeploymentEndTime = DateTime.Now;
                    trackingFile.Percentage = 100;
                    trackingFile.Save();
                    NotifyStatus(id);
                }
            }
        }

        private System.IO.FileInfo ApplySettingsTransformation(string cachePath, ILogger logger, System.IO.FileInfo fileInfo) {
            // Only transform configuration files in the root
            string targetConfig = Path.Combine(cachePath, "web.config");

            if (!String.IsNullOrEmpty(fileInfo.Extension) &&
                fileInfo.FullName.Equals(targetConfig, StringComparison.OrdinalIgnoreCase)) {

                string relativePath = fileInfo.FullName.Substring(cachePath.Length).Trim(Path.DirectorySeparatorChar);
                logger.Log("Applying transform on {0}.", relativePath);

                using (Stream stream = fileInfo.OpenRead()) {
                    using (var reader = new StreamReader(stream)) {
                        XDocument document = Transform(_settingsProvider, reader.ReadToEnd());
                        string tempFile = Path.GetTempFileName();
                        document.Save(tempFile);
                        logger.Log("Generated temporary config transform file ({0}).", tempFile);
                        return new System.IO.FileInfo(tempFile);
                    }
                }
            }

            return fileInfo;
        }

        internal static XDocument Transform(IDeploymentSettingsManager settingsProvider, string content) {
            var configuration = XDocument.Parse(content);

            // Transform the app settings if there's any
            ProcessAppSettings(settingsProvider, configuration);

            ProcessConnectionStrings(settingsProvider, configuration);

            return configuration;
        }

        internal static void ProcessConnectionStrings(IDeploymentSettingsManager settingsProvider, XDocument configuration) {
            IEnumerable<DeploymentSetting> connectionStrings = settingsProvider.GetConnectionStrings();
            if (connectionStrings != null && connectionStrings.Any()) {
                // Add the connection string settings element if needed
                XElement connectionStringsElement = GetElement(configuration.Root, "connectionStrings", createIfNotExists: false);

                // Do nothing if there are no connection strings to replace.
                if (connectionStringsElement == null) {
                    return;
                }

                var entries = (from e in connectionStringsElement.Elements("add")
                               let nameAttr = e.Attribute("name")
                               where nameAttr != null
                               select new {
                                   Name = nameAttr.Value,
                                   Element = e
                               }).ToDictionary(e => e.Name, e => e.Element, StringComparer.OrdinalIgnoreCase);

                foreach (var connectionString in connectionStrings) {
                    // HACK: This is a temporary hack
                    if (connectionString.Key.Equals("All", StringComparison.OrdinalIgnoreCase)) {
                        foreach (var element in entries.Select(e => e.Value)) {
                            element.SetAttributeValue("connectionString", connectionString.Value);
                        }
                        break;
                    }

                    XElement connectionStringEntry;
                    if (!entries.TryGetValue(connectionString.Key, out connectionStringEntry)) {
                        connectionStringEntry = new XElement("add");
                    }

                    connectionStringEntry.SetAttributeValue("name", connectionString.Key);
                    connectionStringEntry.SetAttributeValue("connectionString", connectionString.Value);
                }
            }
        }

        private static XElement GetElement(XElement element, string name, bool createIfNotExists = true) {
            var childElement = element.Element(name);
            if (childElement == null && createIfNotExists) {
                childElement = new XElement(name);
                element.Add(childElement);
            }
            return childElement;
        }

        internal static void ProcessAppSettings(IDeploymentSettingsManager settingsProvider, XDocument configuration) {
            IEnumerable<DeploymentSetting> appSettings = settingsProvider.GetAppSettings();

            if (appSettings != null && appSettings.Any()) {
                XElement appSettingsElement = GetElement(configuration.Root, "appSettings");

                var entries = (from e in appSettingsElement.Elements("add")
                               let keyAttr = e.Attribute("key")
                               where keyAttr != null
                               select new {
                                   Key = keyAttr.Value,
                                   Element = e
                               }).ToDictionary(e => e.Key, e => e.Element, StringComparer.OrdinalIgnoreCase);

                foreach (var setting in appSettings) {
                    XElement appSettingEntry;
                    if (!entries.TryGetValue(setting.Key, out appSettingEntry)) {
                        appSettingEntry = new XElement("add");
                        appSettingsElement.Add(appSettingEntry);
                    }

                    appSettingEntry.SetAttributeValue("key", setting.Key);
                    appSettingEntry.SetAttributeValue("value", setting.Value);
                }
            }
        }

        private void NotifyStatus(string id) {
            var result = GetResult(id);

            if (result == null) {
                result = new DeployResult {
                    Id = id,
                    Status = DeployStatus.Pending
                };
            }

            if (StatusChanged != null) {
                StatusChanged(result);
            }
        }

        private DeploymentStatusFile OpenTrackingFile(string id) {
            return DeploymentStatusFile.Open(GetTrackingFilePath(id));
        }

        private DeploymentStatusFile CreateTrackingFile(string id) {
            return DeploymentStatusFile.Create(GetTrackingFilePath(id));
        }

        private ILogger GetLogger(string id) {
            return new XmlLogger(GetLogPath(id));
        }

        private string GetTrackingFilePath(string id) {
            return Path.Combine(GetRoot(id), "status.xml");
        }

        private string GetCachePath(string id) {
            string path = Path.Combine(GetRoot(id), "cache");
            return FileSystemHelpers.EnsureDirectory(path);
        }

        private string GetLogPath(string id) {
            return Path.Combine(GetRoot(id), "log.xml");
        }

        private string GetRoot(string id) {
            string path = Path.Combine(_environment.DeploymentCachePath, id);
            return FileSystemHelpers.EnsureDirectory(path);
        }

        private string GetActiveDeploymentFilePath() {
            return Path.Combine(_environment.DeploymentCachePath, "active");
        }
    }
}
