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
                StatusText = file.StatusText,
                Complete = file.Complete,
            };
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
            using (var deployStep = profiler.Step("DeploymentManager.Deploy(id)"))
            {
                Build(id, profiler, deployStep);
            }
        }

        public void Deploy()
        {
            IRepository repository = _repositoryManager.GetRepository();

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
                trackingFile.Status = DeployStatus.Building;
                trackingFile.StatusText = String.Format("Building and Deploying {0}...", id);
                trackingFile.Save(_fileSystem);

                ReportStatus(id);

                // Create a deployer
                ISiteBuilder builder = _builderFactory.CreateBuilder();

                buildStep = profiler.Step("Building");

                var context = new DeploymentContext
                {
                    ManifestWriter = GetDeploymentManifestWriter(id),
                    PreviousMainfest = GetActiveDeploymentManifestReader(),
                    Profiler = profiler,
                    Logger = logger,
                    OutputPath = _environment.DeploymentTargetPath
                };

                builder.Build(context)
                       .ContinueWith(task =>
                       {
                           // End the build step
                           buildStep.Dispose();

                           if (task.IsFaulted)
                           {
                               NotifyError(logger, trackingFile, task.Exception);

                               ReportStatus(id);

                               // End the deploy step
                               deployStep.Dispose();
                           }
                           else
                           {
                               // Set the deployment percent to 50% and report status
                               trackingFile.Percentage = 50;
                               trackingFile.Save(_fileSystem);
                               ReportStatus(id);

                               // Run post deployment steps
                               RunPostDeployment(id, profiler, deployStep);

                               // Copy repository (if this is the first push)
                               CopyRepository(id, profiler);
                           }
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
                                           skipHidden: false);
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
        /// - Runs (NJI) node package installer
        /// - Marks the active deployment
        /// </summary>
        private void RunPostDeployment(string id, IProfiler profiler, IDisposable deployStep)
        {
            DeploymentStatusFile trackingFile = null;
            ILogger logger = null;

            try
            {
                trackingFile = OpenTrackingFile(id);
                logger = GetLogger(id);

                using (profiler.Step("Downloading Node packages"))
                {
                    DownloadNodePackages(id, trackingFile, logger);
                }

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

            // Failed to deploy
            trackingFile.Percentage = 100;
            trackingFile.Complete = true;
            trackingFile.Status = DeployStatus.Failed;
            trackingFile.StatusText = trackingFile.Status == DeployStatus.Failed ? logger.GetTopLevelError() : String.Empty;
            trackingFile.DeploymentEndTime = DateTime.Now;
            trackingFile.Save(_fileSystem);
        }

        // Temporary dirty code to install node packages. Switch to real NPM when available
        private void DownloadNodePackages(string id, DeploymentStatusFile trackingFile, ILogger logger)
        {
            var p = new nji.Program();
            p.ModulesDir = Path.Combine(_environment.DeploymentRepositoryTargetPath, "node_modules");
            p.TempDir = Path.Combine(p.ModulesDir, ".tmp");
            p.Logger = logger;
            p.UpdateStatusText = (statusText) =>
            {
                trackingFile.StatusText = statusText;
                trackingFile.Save(_fileSystem);
                ReportStatus(id);
            };

            p.InstallDependencies(_environment.DeploymentTargetPath);
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
