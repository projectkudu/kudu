using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Core.SourceControl;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentManager : IDeploymentManager
    {
        private readonly IServerRepository _serverRepository;
        private readonly ISiteBuilderFactory _builderFactory;
        private readonly IEnvironment _environment;
        private readonly IFileSystem _fileSystem;
        private readonly ITraceFactory _traceFactory;

        private const string StatusFile = "status.xml";
        private const string LogFile = "log.xml";
        private const string ManifestFile = "manifest";
        private const string ActiveDeploymentFile = "active";

        public event Action<DeployResult> StatusChanged;

        public DeploymentManager(IServerRepository serverRepository,
                                 ISiteBuilderFactory builderFactory,
                                 IEnvironment environment,
                                 IFileSystem fileSystem,
                                 ITraceFactory traceFactory)
        {
            _serverRepository = serverRepository;
            _builderFactory = builderFactory;
            _environment = environment;
            _fileSystem = fileSystem;
            _traceFactory = traceFactory;
        }

        private string ActiveDeploymentId
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
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("DeploymentManager.GetResults"))
            {
                return EnumerateResults().ToList();
            }
        }

        public DeployResult GetResult(string id)
        {
            return GetResult(id, ActiveDeploymentId);
        }

        public IEnumerable<LogEntry> GetLogEntries(string id)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("DeploymentManager.GetLogEntries(id)"))
            {
                string path = GetLogPath(id, ensureDirectory: false);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, Resources.Error_NoLogFound, id));
                }

                return new XmlLogger(_fileSystem, path).GetLogEntries().ToList();
            }
        }

        public IEnumerable<LogEntry> GetLogEntryDetails(string id, string entryId)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("DeploymentManager.GetLogEntryDetails(id, entryId)"))
            {
                string path = GetLogPath(id, ensureDirectory: false);

                if (!_fileSystem.File.Exists(path))
                {
                    throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, Resources.Error_NoLogFound, id));
                }

                return new XmlLogger(_fileSystem, path).GetLogEntryDetails(entryId).ToList();
            }
        }

        public void Delete(string id)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("DeploymentManager.Delete(id)"))
            {
                string path = GetRoot(id, ensureDirectory: false);

                if (!_fileSystem.Directory.Exists(path))
                {
                    throw new DirectoryNotFoundException(String.Format(CultureInfo.CurrentCulture, Resources.Error_UnableToDeleteNoDeploymentFound, id));
                }

                if (IsActive(id))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_UnableToDeleteDeploymentActive, id));
                }

                _fileSystem.Directory.Delete(path, true);
            }
        }

        public void Deploy(string id, bool clean)
        {
            ITracer tracer = _traceFactory.GetTracer();
            IDisposable deployStep = null;

            try
            {
                deployStep = tracer.Step("DeploymentManager.Deploy(id)");

                // Check to see if we have a deployment with this id already
                string trackingFilePath = GetStatusFilePath(id, ensureDirectory: false);

                if (!_fileSystem.File.Exists(trackingFilePath))
                {
                    // If we don't then throw
                    throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, Resources.Error_DeployNotFound, id));
                }

                using (tracer.Step("Updating to specific changeset"))
                {
                    // Update to the the specific changeset
                    _serverRepository.Update(id);
                }

                if (clean)
                {
                    tracer.Trace("Cleaning git repository");
                    _serverRepository.Clean();
                }

                // Perform the build deployment of this changeset
                Build(id, tracer, deployStep);
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);

                if (deployStep != null)
                {
                    deployStep.Dispose();
                }

                throw;
            }
        }

        public void Deploy()
        {
            var tracer = _traceFactory.GetTracer();
            IDisposable deployStep = null;

            try
            {
                deployStep = tracer.Step("Deploy");
                PushInfo pushInfo = _serverRepository.GetPushInfo();

                // Something went wrong here since we weren't able to 
                if (pushInfo == null || !pushInfo.Branch.IsMaster)
                {
                    if (pushInfo == null)
                    {
                        tracer.TraceWarning("Push info was null. Post receive hook didn't execute correctly");
                    }
                    else
                    {
                        tracer.Trace("Non-master branch deployed {0}", pushInfo.Branch.Name);
                    }

                    ReportCompleted();
                    deployStep.Dispose();
                    return;
                }

                // Get the pushed branch's id
                string id = pushInfo.Branch.Id;
                // If nothing changed then do nothing
                if (IsActive(id))
                {
                    tracer.Trace("Deployment '{0}' already active", id);

                    ReportCompleted();
                    deployStep.Dispose();
                    return;
                }

                using (tracer.Step("Collecting changeset information"))
                {
                    // Create the status file and store information about the commit
                    DeploymentStatusFile statusFile = CreateStatusFile(id);
                    statusFile.Id = id;
                    ChangeSet changeSet = _serverRepository.GetChangeSet(id);
                    statusFile.Message = changeSet.Message;
                    statusFile.Author = changeSet.AuthorName;
                    statusFile.AuthorEmail = changeSet.AuthorEmail;
                    statusFile.Save(_fileSystem);
                }

                using (tracer.Step("Update to specific changeset"))
                {
                    // Update to the default branch
                    _serverRepository.Update();
                }

                Build(id, tracer, deployStep);
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);

                if (deployStep != null)
                {
                    deployStep.Dispose();
                }

                ReportCompleted();
            }
        }

        /// <summary>
        /// Get result with ActiveDeploymentId
        /// </summary>
        private DeployResult GetResult(string id, string activeDeploymentId)
        {
            var file = OpenStatusFile(id);

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
                StartTime = file.StartTime,
                EndTime = file.EndTime,
                Status = file.Status,
                StatusText = file.StatusText,
                Complete = file.Complete,
                Current = file.Id == activeDeploymentId,
                ReceivedTime = file.ReceivedTime,
                LastSuccessEndTime = file.LastSuccessEndTime
            };
        }

        /// <summary>
        /// Builds and deploys a particular changeset. Puts all build artifacts in a deployments/{id}
        /// </summary>
        private void Build(string id, ITracer tracer, IDisposable deployStep)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException();
            }

            ILogger logger = null;
            DeploymentStatusFile currentStatus = null;
            IDisposable buildStep = null;

            try
            {
                // Remove the old log file for this deployment id
                string logPath = GetLogPath(id);
                FileSystemHelpers.DeleteFileSafe(logPath);

                logger = GetLogger(id);
                ILogger innerLogger = logger.Log(Resources.Log_PreparingDeployment, TrimId(id));

                currentStatus = OpenStatusFile(id);
                currentStatus.Complete = false;
                currentStatus.StartTime = DateTime.Now;
                currentStatus.Status = DeployStatus.Building;
                currentStatus.StatusText = String.Format(CultureInfo.CurrentCulture, Resources.Status_BuildingAndDeploying, id);
                currentStatus.Save(_fileSystem);

                ReportStatus(id);

                ISiteBuilder builder = null;

                try
                {
                    builder = _builderFactory.CreateBuilder(tracer, innerLogger);
                }
                catch(Exception ex)
                {
                    tracer.TraceError(ex);

                    innerLogger.Log(ex);

                    MarkFailed(currentStatus);

                    ReportStatus(id);

                    deployStep.Dispose();

                    return;
                }

                buildStep = tracer.Step("Building");

                var context = new DeploymentContext
                {
                    ManifestWriter = GetDeploymentManifestWriter(id),
                    PreviousMainfest = GetActiveDeploymentManifestReader(),
                    Tracer = tracer,
                    Logger = logger,
                    OutputPath = _environment.DeploymentTargetPath,
                };

                builder.Build(context)
                       .Then(() =>
                       {
                           // End the build step
                           buildStep.Dispose();

                           // Run post deployment steps
                           FinishDeployment(id, tracer, deployStep);
                       })
                       .Catch(ex =>
                       {
                           // End the build step
                           buildStep.Dispose();

                           MarkFailed(currentStatus);

                           ReportStatus(id);

                           // End the deploy step
                           deployStep.Dispose();
                       });
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);

                logger.LogUnexpetedError();

                if (buildStep != null)
                {
                    buildStep.Dispose();
                }

                deployStep.Dispose();
            }
        }

        private void MarkFailed(DeploymentStatusFile currentStatus)
        {
            if (currentStatus == null)
            {
                return;
            }

            currentStatus.Complete = true;
            currentStatus.Status = DeployStatus.Failed;
            currentStatus.StatusText = String.Empty;
            currentStatus.EndTime = DateTime.Now;
            currentStatus.Save(_fileSystem);
        }

        private IEnumerable<DeployResult> EnumerateResults()
        {
            if (!_fileSystem.Directory.Exists(_environment.DeploymentCachePath))
            {
                yield break;
            }

            string activeDeploymentId = ActiveDeploymentId;
            foreach (var id in _fileSystem.Directory.GetDirectories(_environment.DeploymentCachePath))
            {
                var result = GetResult(id, activeDeploymentId);
                if (result != null)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Runs post deployment steps.
        /// - Marks the active deployment
        /// - Sets the complete flag
        /// </summary>
        private void FinishDeployment(string id, ITracer tracer, IDisposable deployStep)
        {
            DeploymentStatusFile currentStatus = null;
            ILogger logger = null;

            try
            {
                currentStatus = OpenStatusFile(id);
                logger = GetLogger(id);

                // Write the active deployment file
                MarkActive(id);

                logger.Log(Resources.Log_DeploymentSuccessful);

                currentStatus.Status = DeployStatus.Success;
                currentStatus.StatusText = String.Empty;
                currentStatus.EndTime = DateTime.Now;
                currentStatus.LastSuccessEndTime = currentStatus.EndTime;
                currentStatus.Save(_fileSystem);
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);

                MarkFailed(currentStatus);

                logger.LogUnexpetedError();
            }
            finally
            {
                // Set the deployment as complete
                currentStatus.Complete = true;
                currentStatus.Save(_fileSystem);

                ReportStatus(id);

                // End the deployment step
                deployStep.Dispose();
            }
        }

        private string TrimId(string id)
        {
            return id.Substring(0, 10);
        }

        private void ReportStatus(string id)
        {
            var result = GetResult(id);

            // There's no status as yet so report as pending
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

        private void MarkActive(string id)
        {
            string activeFilePath = GetActiveDeploymentFilePath();
            File.WriteAllText(activeFilePath, id);
        }

        private DeploymentStatusFile OpenStatusFile(string id)
        {
            return DeploymentStatusFile.Open(_fileSystem, GetStatusFilePath(id, ensureDirectory: false));
        }

        private DeploymentStatusFile CreateStatusFile(string id)
        {
            return DeploymentStatusFile.Create(GetStatusFilePath(id));
        }

        private ILogger GetLogger(string id)
        {
            return new XmlLogger(_fileSystem, GetLogPath(id));
        }

        private string GetStatusFilePath(string id, bool ensureDirectory = true)
        {
            return Path.Combine(GetRoot(id, ensureDirectory), StatusFile);
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
            return Path.Combine(GetRoot(id), ManifestFile);
        }

        private string GetLogPath(string id, bool ensureDirectory = true)
        {
            return Path.Combine(GetRoot(id, ensureDirectory), LogFile);
        }

        private string GetRoot(string id, bool ensureDirectory = true)
        {
            string path = Path.Combine(_environment.DeploymentCachePath, id);

            if (ensureDirectory)
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, path);
            }

            return path;
        }

        private string GetActiveDeploymentFilePath()
        {
            return Path.Combine(_environment.DeploymentCachePath, ActiveDeploymentFile);
        }

        private bool IsActive(string id)
        {
            return id.Equals(ActiveDeploymentId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
