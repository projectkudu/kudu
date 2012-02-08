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
        private readonly IServerRepository _serverRepository;
        private readonly ISiteBuilderFactory _builderFactory;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settingsManager;
        private readonly IFileSystem _fileSystem;
        private readonly IProfilerFactory _profilerFactory;

        public event Action<DeployResult> StatusChanged;

        public DeploymentManager(IServerRepository serverRepository,
                                 ISiteBuilderFactory builderFactory,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settingsManager,
                                 IFileSystem fileSystem,
                                 IProfilerFactory profilerFactory)
        {
            _serverRepository = serverRepository;
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
                StatusText = file.StatusText,
                Complete = file.Complete,
            };
        }

        public IEnumerable<string> GetManifest(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.GetManifest(id)"))
            {
                string path = GetDeploymentManifestPath(id);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new InvalidOperationException(String.Format("No manifest found for '{0}'.", id));
                }

                return new DeploymentManifest(path).GetPaths();
            }
        }

        public IEnumerable<LogEntry> GetLogEntries(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.GetLogEntries(id)"))
            {
                string path = GetLogPath(id);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new InvalidOperationException(String.Format("No log found for '{0}'.", id));
                }

                return new XmlLogger(_fileSystem, path).GetLogEntries().ToList();
            }
        }

        public IEnumerable<LogEntry> GetLogEntryDetails(string id, string entryId)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.GetLogEntryDetails(id, entryId)"))
            {
                string path = GetLogPath(id);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new InvalidOperationException(String.Format("No log found for '{0}'.", id));
                }

                return new XmlLogger(_fileSystem, path).GetLogEntryDetails(entryId).ToList();
            }
        }

        public void Delete(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("DeploymentManager.Delete(id)"))
            {
                string path = GetRoot(id);

                _fileSystem.Directory.Delete(path, true);
            }
        }

        public void Deploy(string id)
        {
            var profiler = _profilerFactory.GetProfiler();
            IDisposable deployStep = null;

            try
            {
                deployStep = profiler.Step("DeploymentManager.Deploy(id)");

                // Check to see if we have a deployment with this id already
                string trackingFilePath = GetTrackingFilePath(id);

                if (!_fileSystem.File.Exists(trackingFilePath))
                {
                    // If we don't then throw
                    throw new InvalidOperationException(String.Format("Unable to deploy '{0}'. No deployments found.", id));
                }

                using (profiler.Step("Updating to specific changeset"))
                {
                    // Update to the the specific changeset
                    _serverRepository.Update(id);
                }

                // Perform the build deployment of this changeset
                Build(id, profiler, deployStep);
            }
            catch
            {
                if (deployStep != null)
                {
                    deployStep.Dispose();
                }
            }

        }

        public void Deploy()
        {
            var profiler = _profilerFactory.GetProfiler();
            IDisposable deployStep = null;

            try
            {
                deployStep = profiler.Step("Deploy");
                PushInfo pushInfo = _serverRepository.GetPushInfo();

                // Something went wrong here since we weren't able to 
                if (pushInfo == null || !pushInfo.Branch.IsMaster)
                {
                    ReportCompleted();
                    deployStep.Dispose();
                    return;
                }

                using (profiler.Step("Update to specific changeset"))
                {
                    // Update to the default branch
                    _serverRepository.Update();
                }

                // Get the pointer to the default branch
                string id = _serverRepository.CurrentId;

                // If nothing changed then do nothing
                if (id.Equals(ActiveDeploymentId, StringComparison.OrdinalIgnoreCase))
                {
                    ReportCompleted();
                    deployStep.Dispose();
                    return;
                }

                using (profiler.Step("Collecting changeset information"))
                {
                    // Create the tracking file and store information about the commit
                    DeploymentStatusFile statusFile = CreateTrackingFile(id);
                    statusFile.Id = id;
                    ChangeSet changeSet = _serverRepository.GetChangeSet(id);
                    statusFile.Message = changeSet.Message;
                    statusFile.Author = changeSet.AuthorName;
                    statusFile.AuthorEmail = changeSet.AuthorEmail;
                    statusFile.Save(_fileSystem);
                }

                Build(id, profiler, deployStep);
            }
            catch
            {
                if (deployStep != null)
                {
                    deployStep.Dispose();
                }

                ReportCompleted();
            }
        }

        /// <summary>
        /// Builds and deploys a particular changeset. Puts all build artifacts in a deployments/{id}
        /// </summary>
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
            IDisposable buildStep = null;

            try
            {
                // Get the logger for this id
                string logPath = GetLogPath(id);
                FileSystemHelpers.DeleteFileSafe(logPath);

                ReportStatus(id);

                logger = GetLogger(id);
                innerLogger = logger.Log("Preparing deployment for {0}.", id);

                trackingFile = OpenTrackingFile(id);
                trackingFile.Complete = false;
                trackingFile.Status = DeployStatus.Building;
                trackingFile.StatusText = String.Format("Building and Deploying {0}...", id);
                trackingFile.Save(_fileSystem);

                ReportStatus(id);

                // Create a deployer
                ISiteBuilder builder = _builderFactory.CreateBuilder();

                buildStep = profiler.Step("Building");

                var progressReporter = new ProgressReporter(statusText =>
                {
                    trackingFile.StatusText = statusText;
                    trackingFile.Save(_fileSystem);
                    ReportStatus(id);
                });

                var context = new DeploymentContext
                {
                    ManifestWriter = GetDeploymentManifestWriter(id),
                    PreviousMainfest = GetActiveDeploymentManifestReader(),
                    Profiler = profiler,
                    Logger = logger,
                    OutputPath = _environment.DeploymentTargetPath,
                    ProgressReporter = progressReporter
                };

                builder.Build(context)
                       .Then(() =>
                       {
                           // End the build step
                           buildStep.Dispose();
                           // Set the deployment percent to 50% and report status
                           trackingFile.Percentage = 50;
                           trackingFile.Save(_fileSystem);
                           ReportStatus(id);

                           // Run post deployment steps
                           RunPostDeploymentSteps(id, profiler, deployStep);

                           // Copy repository (if this is the first push)
                           CopyRepository(id, profiler);
                       })
                       .Catch(ex =>
                       {
                           NotifyError(logger, trackingFile, ex);

                           ReportStatus(id);

                           // End the deploy step
                           deployStep.Dispose();
                       });
            }
            catch (Exception e)
            {
                if (innerLogger != null)
                {
                    NotifyError(innerLogger, trackingFile, e);
                    innerLogger.Log(e);

                    ReportStatus(id);
                }

                if (buildStep != null)
                {
                    buildStep.Dispose();
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

        /// <summary>
        /// Copy the repository from the temp repository path to the repository path.
        /// Only happens on first deployment.
        /// </summary>
        private void CopyRepository(string id, IProfiler profiler)
        {
            ILogger logger = null;
            DeploymentStatusFile trackingFile = null;

            try
            {
                logger = GetLogger(id);
                trackingFile = OpenTrackingFile(id);

                // The repository has already been copied
                if (_environment.RepositoryType != RepositoryType.None)
                {
                    return;
                }

                using (profiler.Step("Copying files to repository"))
                {
                    // Copy the repository from the temporary path to the repository target path
                    FileSystemHelpers.Copy(_environment.DeploymentRepositoryPath,
                                           _environment.DeploymentRepositoryTargetPath,
                                           skipScmFolder: false);
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Log(ex);
                }
            }
            finally
            {
                if (trackingFile != null)
                {
                    trackingFile.Complete = true;
                    trackingFile.Save(_fileSystem);
                }

                ReportStatus(id);
            }
        }

        /// <summary>
        /// Runs post deployment steps.
        /// - Marks the active deployment
        /// </summary>
        private void RunPostDeploymentSteps(string id, IProfiler profiler, IDisposable deployStep)
        {
            DeploymentStatusFile trackingFile = null;
            ILogger logger = null;

            try
            {
                trackingFile = OpenTrackingFile(id);
                logger = GetLogger(id);

                // Write the active deployment file
                string activeFilePath = GetActiveDeploymentFilePath();
                File.WriteAllText(activeFilePath, id);

                logger.Log("Deployment successful.");

                trackingFile.Status = DeployStatus.Success;
                trackingFile.StatusText = String.Empty;
                trackingFile.DeploymentEndTime = DateTime.Now;
                trackingFile.Percentage = 100;
                trackingFile.Save(_fileSystem);
            }
            catch (Exception ex)
            {
                if (logger != null && trackingFile != null)
                {
                    NotifyError(logger, trackingFile, ex);
                }
            }
            finally
            {
                ReportStatus(id);

                // End the deployment step
                deployStep.Dispose();
            }
        }

        private void NotifyError(ILogger logger, DeploymentStatusFile trackingFile, Exception exception)
        {
            logger.Log("Deployment failed.", LogEntryType.Error);

            if (trackingFile != null)
            {
                // Failed to deploy
                trackingFile.Percentage = 100;
                trackingFile.Complete = true;
                trackingFile.Status = DeployStatus.Failed;
                trackingFile.StatusText = trackingFile.Status == DeployStatus.Failed ? logger.GetTopLevelError() : String.Empty;
                trackingFile.DeploymentEndTime = DateTime.Now;
                trackingFile.Save(_fileSystem);
            }
        }

        private void ReportStatus(string id)
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

        private void ReportCompleted()
        {
            if (StatusChanged != null)
            {
                StatusChanged(new DeployResult
                {
                    Complete = true
                });
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

        private IDeploymentManifestWriter GetDeploymentManifestWriter(string id)
        {
            return new DeploymentManifest(GetDeploymentManifestPath(id));
        }

        private IDeploymentManifestReader GetActiveDeploymentManifestReader()
        {
            string id = ActiveDeploymentId;

            if (String.IsNullOrEmpty(id))
            {
                return null;
            }

            return new DeploymentManifest(GetDeploymentManifestPath(id));
        }

        private string GetDeploymentManifestPath(string id)
        {
            return Path.Combine(GetRoot(id), "manifest");
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
