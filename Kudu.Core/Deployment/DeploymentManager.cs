using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager {
        private readonly IRepositoryManager _repositoryManager;
        private readonly ISiteBuilderFactory _builderFactory;
        private readonly IEnvironment _environment;
        private readonly IDeploymentNotifier _notifier;

        public DeploymentManager(IRepositoryManager repositoryManager,
                                 ISiteBuilderFactory builderFactory,
                                 IEnvironment environment,
                                 IDeploymentNotifier notifier) {
            _repositoryManager = repositoryManager;
            _builderFactory = builderFactory;
            _environment = environment;
            _notifier = notifier;
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
                return new DeployResult {
                    Id = file.Id,
                    Status = DeployStatus.Pending
                };
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

                trackingFile.StatusText = "Deploying to webroot...";
                trackingFile.Save();
                NotifyStatus(id);

                logger.Log("Copying files to {0}.", _environment.DeploymentTargetPath);

                // Copy to target
                FileSystemHelpers.SmartCopy(cachePath, _environment.DeploymentTargetPath, skipOldFiles);

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

        private void NotifyStatus(string id) {
            _notifier.NotifyStatus(GetResult(id));
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
