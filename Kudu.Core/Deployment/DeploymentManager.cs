using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts;
using Kudu.Core.Infrastructure;
using Kudu.Core.Performance;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment
{
    public class DeploymentManager : IDeploymentManager
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly ISiteBuilderFactory _builderFactory;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settingsManager;
        private readonly IFileSystem _fileSystem;
        private readonly IProfilerFactory _profilerFactory;

        public event Action<DeployResult> StatusChanged;

        public DeploymentManager(IRepositoryManager repositoryManager,
                                 ISiteBuilderFactory builderFactory,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settingsManager,
                                 IFileSystem fileSystem,
                                 IProfilerFactory profilerFactory)
        {
            _repositoryManager = repositoryManager;
            _builderFactory = builderFactory;
            _environment = environment;
            _settingsManager = settingsManager;
            _fileSystem = fileSystem;
            _profilerFactory = profilerFactory;
        }

        public string ActiveDeploymentId
        {
            get
            {
                string path = GetActiveDeploymentFilePath();
                if (_fileSystem.File.Exists(path))
                {
                    return _fileSystem.File.ReadAllText(path);
                }
                return null;
            }
        }

        public IEnumerable<DeployResult> GetResults()
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.GetResults"))
            {
                return EnumerateResults().ToList();
            }
        }

        public DeployResult GetResult(string id)
        {
            var profiler = _profilerFactory.GetProfiler();

            var file = OpenTrackingFile(id);

            if (file == null)
            {
                return null;
            }

            return new DeployResult
            {
                Id = file.Id,
                Author = file.Author,
                AuthorEmail = file.AuthorEmail,
                Message = file.Message,
                DeployStartTime = file.DeploymentStartTime,
                DeployEndTime = file.DeploymentEndTime,
                Status = file.Status,
                Percentage = file.Percentage,
                StatusText = file.StatusText
            };
        }

        public IEnumerable<LogEntry> GetLogEntries(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.GetLogEntries"))
            {
                string path = GetLogPath(id);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new InvalidOperationException(String.Format("No log found for '{0}'.", id));
                }

                return new XmlLogger(_fileSystem, path).GetLogEntries().ToList();
            }
        }

        public IEnumerable<LogEntry> GetLogEntryDetails(string id, string dateId)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.GetLogEntryDetails"))
            {
                string path = GetLogPath(id);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new InvalidOperationException(String.Format("No log found for '{0}'.", id));
                }

                return new XmlLogger(_fileSystem, path).GetLogEntryDetails(dateId).ToList();
            }
        }

        public void Delete(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.Delete"))
            {
                // TODO: Check for exceptions related to Delete.
                _fileSystem.Directory.Delete(GetRoot((id)), true);
            }
        }

        public void Deploy(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.Deploy(id)"))
            {
                string cachePath = GetCachePath(id);

                if (!_fileSystem.Directory.Exists(cachePath))
                {
                    throw new InvalidOperationException(String.Format("Unable to deploy '{0}'. No deployments found.", id));
                }

                // We don't want to skip old files here since we might be going back in time
                DeployToTarget(id, profiler, DisposableAction.Noop, skipOldFiles: false);
            }
        }

        public void Deploy()
        {
            var repository = _repositoryManager.GetRepository();

            if (repository == null)
            {
                return;
            }

            var profiler = _profilerFactory.GetProfiler();
            var deployStep = profiler.Step("Deploy");

            string id = repository.CurrentId;

            using (profiler.Step("Update to specific changeset"))
            {
                if (String.IsNullOrEmpty(id))
                {
                    id = repository.GetChanges(0, 1).Single().Id;
                    repository.Update(id);
                }

                Branch activeBranch = (from b in repository.GetBranches()
                                       let change = repository.GetDetails(b.Id)
                                       orderby change.ChangeSet.Timestamp descending
                                       select b).FirstOrDefault();

                if (activeBranch != null)
                {
                    // Only deploy if the active branch is the master branch
                    if (!activeBranch.IsMaster)
                    {
                        return;
                    }

                    repository.Update(activeBranch.Name);
                    id = activeBranch.Id;
                }
                else
                {
                    repository.Update(id);
                }
            }

            // Create the tracking file and store information about the commit
            DeploymentStatusFile statusFile = CreateTrackingFile(id);
            statusFile.Id = id;
            var details = repository.GetDetails(id);
            statusFile.Message = details.ChangeSet.Message;
            statusFile.Author = details.ChangeSet.AuthorName;
            statusFile.AuthorEmail = details.ChangeSet.AuthorEmail;
            statusFile.Save(_fileSystem);

            Build(id, profiler, deployStep);
        }

        public void Build(string id)
        {
            Build(id, NullProfiler.Instance, DisposableAction.Noop);
        }

        private void Build(string id, IProfiler profiler, IDisposable deployStep)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException();
            }

            // TODO: Make sure if the if is a valid changeset            
            ILogger logger = null;
            DeploymentStatusFile trackingFile = null;
            ILogger innerLogger = null;

            try
            {
                // Get the logger for this id
                string logPath = GetLogPath(id);
                FileSystemHelpers.DeleteFileSafe(logPath);
                NotifyStatus(id);

                logger = GetLogger(id);

                innerLogger = logger.Log("Preparing deployment for {0}.", id);

                // Put bits in the cache folder
                // string cachePath = GetCachePath(id);                

                // The initial status
                trackingFile = OpenTrackingFile(id);
                trackingFile.Status = DeployStatus.Building;
                trackingFile.StatusText = String.Format("Building {0}...", id);
                trackingFile.Save(_fileSystem);

                NotifyStatus(id);

                // Create a deployer
                ISiteBuilder builder = _builderFactory.CreateBuilder();

                var buildStep = profiler.Step("Building");

                builder.Build(_environment.DeploymentTargetPath, logger)
                       .ContinueWith(t =>
                       {
                           buildStep.Dispose();

                           if (t.IsFaulted)
                           {
                               // We need to read the exception so the process doesn't go down
                               NotifyError(logger, trackingFile, t.Exception);

                               NotifyStatus(id);

                               // End the deploy step
                               deployStep.Dispose();
                           }
                           else
                           {
                               trackingFile.Percentage = 50;
                               trackingFile.Save(_fileSystem);
                               NotifyStatus(id);

                               DeployToTarget(id, profiler, deployStep: deployStep);
                           }
                       });
            }
            catch (Exception e)
            {
                if (innerLogger != null)
                {
                    NotifyError(innerLogger, trackingFile, e);
                    innerLogger.Log(e);

                    NotifyStatus(id);
                }

                deployStep.Dispose();
            }
        }

        private IEnumerable<DeployResult> EnumerateResults()
        {
            if (!_fileSystem.Directory.Exists(_environment.DeploymentCachePath))
            {
                yield break;
            }

            foreach (var id in _fileSystem.Directory.GetDirectories(_environment.DeploymentCachePath))
            {
                var result = GetResult(id);
                if (result != null)
                {
                    yield return result;
                }
            }
        }

        private void NotifyError(ILogger logger, DeploymentStatusFile trackingFile, Exception exception)
        {
            logger.Log("Deployment failed.", LogEntryType.Error);

            // Failed to deploy
            trackingFile.Percentage = 100;
            trackingFile.Status = DeployStatus.Failed;
            trackingFile.StatusText = trackingFile.Status == DeployStatus.Failed ? logger.GetTopLevelError() : String.Empty;
            trackingFile.DeploymentEndTime = DateTime.Now;
            trackingFile.Save(_fileSystem);
        }

        private void DeployToTarget(string id, IProfiler profiler, IDisposable deployStep, bool skipOldFiles = true)
        {
            DeploymentStatusFile trackingFile = null;
            ILogger logger = null;
            ILogger innerLogger = null;
            try
            {
                trackingFile = OpenTrackingFile(id);
                logger = GetLogger(id);

                //trackingFile.Percentage = 50;
                //trackingFile.Status = DeployStatus.Deploying;
                //trackingFile.StatusText = "Deploying to webroot...";
                //trackingFile.Save(_fileSystem);
                //NotifyStatus(id);

                //innerLogger = logger.Log("Copying files to webroot.");

                //string deploymentId = ActiveDeploymentId;
                //string activeDeploymentPath = String.IsNullOrEmpty(deploymentId) ? null : GetCachePath(deploymentId);

                //using (profiler.Step("Copying files to webroot"))
                //{
                //    // Copy to target
                //    FileSystemHelpers.SmartCopy(activeDeploymentPath, cachePath, _environment.DeploymentTargetPath, skipOldFiles);
                //}

                using (profiler.Step("Downloading Node packages"))
                {
                    DownloadNodePackages(id, trackingFile, logger);
                }

                trackingFile.Status = DeployStatus.Success;
                trackingFile.StatusText = String.Empty;

                // Write the active deployment file
                string activeFilePath = GetActiveDeploymentFilePath();
                File.WriteAllText(activeFilePath, id);

                logger.Log("Deployment successful.");
            }
            catch (Exception e)
            {
                if (trackingFile != null)
                {
                    trackingFile.Status = DeployStatus.Failed;
                }

                if (innerLogger != null)
                {
                    innerLogger.Log("Deploying to web root failed.", LogEntryType.Error);
                    innerLogger.Log(e);
                }
            }
            finally
            {
                if (trackingFile != null)
                {
                    trackingFile.DeploymentEndTime = DateTime.Now;
                    trackingFile.StatusText = trackingFile.Status == DeployStatus.Failed ? logger.GetTopLevelError() : String.Empty;
                    trackingFile.Percentage = 100;
                    trackingFile.Save(_fileSystem);
                    NotifyStatus(id);
                }

                deployStep.Dispose();
            }
        }

        // Temporary dirty code to install node packages. Switch to real NPM when available
        private void DownloadNodePackages(string id, DeploymentStatusFile trackingFile, ILogger logger)
        {
            var p = new nji.Program();
            p.ModulesDir = Path.Combine(_environment.DeploymentTargetPath, "node_modules");
            p.TempDir = Path.Combine(p.ModulesDir, ".tmp");
            p.Logger = logger;
            p.UpdateStatusText = (statusText) =>
            {
                trackingFile.StatusText = statusText;
                trackingFile.Save(_fileSystem);
                NotifyStatus(id);
            };

            p.InstallDependencies(_environment.DeploymentTargetPath);
        }

        private void NotifyStatus(string id)
        {
            var result = GetResult(id);

            if (result == null)
            {
                result = new DeployResult
                {
                    Id = id,
                    Status = DeployStatus.Pending
                };
            }

            if (StatusChanged != null)
            {
                StatusChanged(result);
            }
        }

        private DeploymentStatusFile OpenTrackingFile(string id)
        {
            return DeploymentStatusFile.Open(_fileSystem, GetTrackingFilePath(id));
        }

        private DeploymentStatusFile CreateTrackingFile(string id)
        {
            return DeploymentStatusFile.Create(GetTrackingFilePath(id));
        }

        private ILogger GetLogger(string id)
        {
            return new XmlLogger(_fileSystem, GetLogPath(id));
        }

        private string GetTrackingFilePath(string id)
        {
            return Path.Combine(GetRoot(id), "status.xml");
        }

        private string GetCachePath(string id)
        {
            string path = Path.Combine(GetRoot(id), "cache");
            return FileSystemHelpers.EnsureDirectory(_fileSystem, path);
        }

        private string GetLogPath(string id)
        {
            return Path.Combine(GetRoot(id), "log.xml");
        }

        private string GetRoot(string id)
        {
            string path = Path.Combine(_environment.DeploymentCachePath, id);
            return FileSystemHelpers.EnsureDirectory(_fileSystem, path);
        }

        private string GetActiveDeploymentFilePath()
        {
            return Path.Combine(_environment.DeploymentCachePath, "active");
        }
    }
}
