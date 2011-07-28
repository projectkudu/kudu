using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment {
    public class DeploymentManager : IDeploymentManager {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IDeployerFactory _deployerFactory;
        private readonly IEnvironment _environment;

        public DeploymentManager(IRepositoryManager repositoryManager,
                                 IDeployerFactory deployerFactory,
                                 IEnvironment environment) {
            _repositoryManager = repositoryManager;
            _deployerFactory = deployerFactory;
            _environment = environment;
        }

        public IEnumerable<DeployResult> GetResults() {
            if (!Directory.Exists(_environment.DeploymentCachePath)) {
                yield break;
            }

            foreach (var id in Directory.EnumerateDirectories(_environment.DeploymentCachePath)) {
                yield return GetResult(id);
            }
        }

        public DeployResult GetResult(string id) {
            var file = OpenTrackingFile(id);

            if (file == null) {
                return null;
            }

            return new DeployResult {
                Id = file.Id,
                DeployTime = file.DeployTime,
                Status = file.Status,
                Percentage = file.Percentage,
                StatusText = file.StatusText
            };
        }

        public string GetLog(string id) {
            string path = GetLogPath(id);
            if (!File.Exists(path)) {
                throw new InvalidOperationException(String.Format("No log found for '{0}'.", id));
            }

            return File.ReadAllText(path);
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

            Branch activeBranch = repository.GetBranches().FirstOrDefault(b => b.Active);
            string id = repository.CurrentId;

            if (activeBranch != null) {
                repository.Update(activeBranch.Name);
            }
            else {
                repository.Update(id);
            }

            Build(id);
        }

        public void Build(string id) {
            // Put bits in the cache folder
            string cachePath = GetCachePath(id);
            Directory.CreateDirectory(cachePath);

            // The initial status
            DeploymentTrackingFile trackingFile = CreateTrackingFile(id);
            trackingFile.Id = id;
            trackingFile.Status = DeployStatus.Pending;
            trackingFile.StatusText = String.Format("Building {0}...", id);
            trackingFile.Save();

            // Create a logger over the text file
            var writer = new StreamWriter(GetLogPath(id));

            // Create a deployer
            IDeployer deployer = _deployerFactory.CreateDeployer();

            deployer.Deploy(cachePath, new Logger(writer))
                    .ContinueWith(t => {
                        if (t.IsFaulted) {
                            // failed to deploy
                            trackingFile.Percentage = 100;
                            trackingFile.Status = DeployStatus.Failed;
                            trackingFile.StatusText = t.Exception.GetBaseException().Message;
                            trackingFile.Save();
                        }
                        else {
                            trackingFile.Percentage = 50;
                            trackingFile.Save();
                            DeployToTarget(id);
                        }

                        writer.Close();
                    });
        }

        private void DeployToTarget(string id, bool skipOldFiles = true) {
            DeploymentTrackingFile trackingFile = null;

            try {
                string cachePath = GetCachePath(id);
                trackingFile = OpenTrackingFile(id);

                trackingFile.StatusText = "Deploying to webroot...";
                trackingFile.Save();

                // Copy to target
                DeploymentHelpers.SmartCopy(cachePath, _environment.DeploymentTargetPath, skipOldFiles);

                trackingFile.Status = DeployStatus.Done;
                trackingFile.StatusText = String.Empty;
            }
            catch (Exception e) {
                if (trackingFile != null) {
                    trackingFile.Status = DeployStatus.Failed;
                    trackingFile.StatusText = e.GetBaseException().Message;
                }
            }
            finally {
                if (trackingFile != null) {
                    trackingFile.Percentage = 100;
                    trackingFile.Save();
                }
            }
        }

        private DeploymentTrackingFile OpenTrackingFile(string id) {
            return DeploymentTrackingFile.Open(GetTrackingFilePath(id));
        }

        private DeploymentTrackingFile CreateTrackingFile(string id) {
            return DeploymentTrackingFile.Create(GetTrackingFilePath(id));
        }

        private string GetTrackingFilePath(string id) {
            return Path.Combine(GetRoot(id), "status.xml");
        }

        private string GetCachePath(string id) {
            return Path.Combine(GetRoot(id), "cache");
        }

        private string GetLogPath(string id) {
            return Path.Combine(GetRoot(id), "log.txt");
        }

        private string GetRoot(string id) {
            return Path.Combine(_environment.DeploymentCachePath, id);
        }

        private class Logger : ILogger {
            private readonly TextWriter _writer;
            public Logger(TextWriter writer) {
                _writer = writer;
            }

            public void WriteLog(string value) {
                _writer.Write(value);
            }
        }
    }
}
