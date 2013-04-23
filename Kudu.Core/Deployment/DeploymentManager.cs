using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentManager : IDeploymentManager
    {
        private static readonly Random _random = new Random();

        private readonly ISiteBuilderFactory _builderFactory;
        private readonly IEnvironment _environment;
        private readonly IFileSystem _fileSystem;
        private readonly ITraceFactory _traceFactory;
        private readonly IOperationLock _deploymentLock;
        private readonly ILogger _globalLogger;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IDeploymentStatusManager _status;

        private const string LogFile = "log.xml";
        private const string ManifestFile = "manifest";
        private const string TemporaryDeploymentIdPrefix = "temp-";
        public const int MaxSuccessDeploymentResults = 10;

        public DeploymentManager(ISiteBuilderFactory builderFactory,
                                 IEnvironment environment,
                                 IFileSystem fileSystem,
                                 ITraceFactory traceFactory,
                                 IDeploymentSettingsManager settings,
                                 IDeploymentStatusManager status,
                                 IOperationLock deploymentLock,
                                 ILogger globalLogger)
        {
            _builderFactory = builderFactory;
            _environment = environment;
            _fileSystem = fileSystem;
            _traceFactory = traceFactory;
            _deploymentLock = deploymentLock;
            _globalLogger = globalLogger ?? NullLogger.Instance;
            _settings = settings;
            _status = status;
        }

        private bool IsDeploying
        {
            get
            {
                return _deploymentLock.IsHeld;
            }
        }

        public IEnumerable<DeployResult> GetResults()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("DeploymentManager.GetResults"))
            {
                // Order the results by date (newest first). Previously, we supported OData to allow
                // arbitrary queries, but that was way overkill and brought in too many large binaries.
                IEnumerable<DeployResult> results = EnumerateResults().OrderByDescending(t => t.ReceivedTime).ToList();

                results = PurgeDeployments(results);

                return results;
            }
        }

        public DeployResult GetResult(string id)
        {
            return GetResult(id, _status.ActiveDeploymentId, IsDeploying);
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

                VerifyDeployment(id, IsDeploying);

                var logger = new XmlLogger(_fileSystem, path);
                List<LogEntry> entries = logger.GetLogEntries().ToList();

                // Determine if there's details to show at all
                foreach (var e in entries)
                {
                    e.HasDetails = logger.GetLogEntryDetails(e.Id).Any();
                }

                return entries;
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

                VerifyDeployment(id, IsDeploying);

                var logger = new XmlLogger(_fileSystem, path);

                return logger.GetLogEntryDetails(entryId).ToList();
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

                _status.Delete(id);
            }
        }

        public void Deploy(IRepository repository, ChangeSet changeSet, string deployer, bool clean, bool needFileUpdate)
        {
            ITracer tracer = _traceFactory.GetTracer();
            IDisposable deployStep = null;
            ILogger innerLogger = null;
            string targetBranch = null;

            // If we don't get a changeset, find out what branch we should be deploying and get the commit ID from it
            if (changeSet == null)
            {
                targetBranch = _settings.GetBranch();

                changeSet = repository.GetChangeSet(targetBranch);
            }

            string id = changeSet.Id;
            IDeploymentStatusFile statusFile = null;
            try
            {
                deployStep = tracer.Step("DeploymentManager.Deploy(id)");

                // Remove the old log file for this deployment id
                string logPath = GetLogPath(id);
                FileSystemHelpers.DeleteFileSafe(logPath);

                statusFile = GetOrCreateStatusFile(changeSet, tracer, deployer);
                statusFile.MarkPending();

                ILogger logger = GetLogger(changeSet.Id);

                if (needFileUpdate)
                {
                    using (tracer.Step("Updating to specific changeset"))
                    {
                        innerLogger = logger.Log(Resources.Log_UpdatingBranch, targetBranch ?? id);

                        using (var writer = new ProgressWriter())
                        {
                            // Update to the the specific changeset
                            repository.ClearLock();
                            repository.Update(id);
                        }
                    }
                }

                using (tracer.Step("Updating submodules"))
                {
                    innerLogger = logger.Log(Resources.Log_UpdatingSubmodules);

                    repository.UpdateSubmodules();
                }

                if (clean)
                {
                    tracer.Trace("Cleaning {0} repository", repository.RepositoryType);

                    innerLogger = logger.Log(Resources.Log_CleaningRepository, repository.RepositoryType);

                    repository.Clean();
                }

                // set to null as Build() below takes over logging
                innerLogger = null;

                // Perform the build deployment of this changeset
                Build(id, tracer, deployStep);
            }
            catch (Exception ex)
            {
                if (innerLogger != null)
                {
                    innerLogger.Log(ex);
                }

                if (statusFile != null)
                {
                    statusFile.MarkFailed();
                }

                tracer.TraceError(ex);

                if (deployStep != null)
                {
                    deployStep.Dispose();
                }

                throw;
            }
        }

        public IDisposable CreateTemporaryDeployment(string statusText, out ChangeSet tempChangeSet, ChangeSet changeSet = null, string deployedBy = null)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("Creating temporary deployment"))
            {
                changeSet = changeSet != null && changeSet.IsTemporary ? changeSet : CreateTemporaryChangeSet();
                IDeploymentStatusFile statusFile = _status.Create(changeSet.Id);
                statusFile.Id = changeSet.Id;
                statusFile.Message = changeSet.Message;
                statusFile.Author = changeSet.AuthorName;
                statusFile.Deployer = deployedBy;
                statusFile.AuthorEmail = changeSet.AuthorEmail;
                statusFile.Status = DeployStatus.Pending;
                statusFile.StatusText = statusText;
                statusFile.IsTemporary = changeSet.IsTemporary;
                statusFile.Save();
            }

            tempChangeSet = changeSet;

            // Return a handle that deletes the deployment on dispose.
            return new DisposableAction(() =>
            {
                if (changeSet.IsTemporary)
                {
                    _status.Delete(changeSet.Id);
                }
            });
        }

        public static ChangeSet CreateTemporaryChangeSet(string authorName = null, string authorEmail = null, string message = null)
        {
            string unknown = Resources.Deployment_UnknownValue;
            return new ChangeSet(GenerateTemporaryId(), authorName ?? unknown, authorEmail ?? unknown, message ?? unknown, DateTimeOffset.MinValue)
            {
                IsTemporary = true
            };
        }

        // since the expensive part (reading all files) is done,
        // we opt for simplicity rather than performance when purging.
        // the input must be in desc order of ReceivedTime (newest first).
        internal IEnumerable<DeployResult> PurgeDeployments(IEnumerable<DeployResult> results)
        {
            if (results.Any())
            {
                var toDelete = new List<DeployResult>();
                toDelete.AddRange(GetPurgeTemporaryDeployments(results));
                toDelete.AddRange(GetPurgeFailedDeployments(results));
                toDelete.AddRange(this.GetPurgeObsoleteDeployments(results));

                if (toDelete.Any())
                {
                    var tracer = _traceFactory.GetTracer();
                    using (tracer.Step("Purge deployment items"))
                    {
                        foreach (DeployResult delete in toDelete)
                        {
                            _status.Delete(delete.Id);

                            tracer.Trace("Remove {0}, {1}, received at {2}",
                                         delete.Id.Substring(0, Math.Min(delete.Id.Length, 9)),
                                         delete.Status,
                                         delete.ReceivedTime);
                        }
                    }

                    results = results.Where(r => !toDelete.Any(i => i.Id == r.Id));
                }
            }

            return results;
        }

        private static IEnumerable<DeployResult> GetPurgeTemporaryDeployments(IEnumerable<DeployResult> results)
        {
            var toDelete = new List<DeployResult>();

            // more than one pending/building, remove all temporary pending
            var pendings = results.Where(r => r.Status != DeployStatus.Failed && r.Status != DeployStatus.Success);
            if (pendings.Count() > 1)
            {
                if (pendings.Any(r => !r.IsTemporary))
                {
                    // if there is non-temporary, remove all pending temporary
                    toDelete.AddRange(pendings.Where(r => r.IsTemporary));
                }
                else
                {
                    if (pendings.First().Id == results.First().Id)
                    {
                        pendings = pendings.Skip(1);
                    }

                    // if first item is not pending temporary, remove all pending temporary
                    toDelete.AddRange(pendings);
                }
            }

            return toDelete;
        }

        private static IEnumerable<DeployResult> GetPurgeFailedDeployments(IEnumerable<DeployResult> results)
        {
            var toDelete = new List<DeployResult>();

            // if one or more fail that never succeeded, only keep latest first one.
            var fails = results.Where(r => r.Status == DeployStatus.Failed && r.LastSuccessEndTime == null);
            if (fails.Any())
            {
                if (fails.First().Id == results.First().Id)
                {
                    fails = fails.Skip(1);
                }

                toDelete.AddRange(fails);
            }

            return toDelete;
        }

        private IEnumerable<DeployResult> GetPurgeObsoleteDeployments(IEnumerable<DeployResult> results)
        {
            var toDelete = new List<DeployResult>();

            // limit number of ever-success items
            // the assumption is user will no longer be interested on these items
            var succeed = results.Where(r => r.LastSuccessEndTime != null);
            if (succeed.Count() > MaxSuccessDeploymentResults)
            {
                // always maintain active and inprogress item
                var activeId = _status.ActiveDeploymentId;
                var purge = succeed.Skip(MaxSuccessDeploymentResults).Where(r =>
                    r.Id != activeId && (r.Status == DeployStatus.Failed || r.Status == DeployStatus.Success));

                toDelete.AddRange(purge);
            }

            return toDelete;
        }

        private static string GenerateTemporaryId(int lenght = 8)
        {
            const string HexChars = "0123456789abcdfe";

            var strb = new StringBuilder();
            strb.Append(TemporaryDeploymentIdPrefix);
            for (int i = 0; i < lenght; ++i)
            {
                strb.Append(HexChars[_random.Next(HexChars.Length)]);
            }

            return strb.ToString();
        }

        internal IDeploymentStatusFile GetOrCreateStatusFile(ChangeSet changeSet, ITracer tracer, string deployer)
        {
            string id = changeSet.Id;

            using (tracer.Step("Collecting changeset information"))
            {
                // Check if the status file already exists. This would happen when we're doing a redeploy
                IDeploymentStatusFile statusFile = _status.Open(id);
                if (statusFile == null)
                {
                    // Create the status file and store information about the commit
                    statusFile = _status.Create(id);
                }
                statusFile.Message = changeSet.Message;
                statusFile.Author = changeSet.AuthorName;
                statusFile.Deployer = deployer;
                statusFile.AuthorEmail = changeSet.AuthorEmail;
                statusFile.Save();

                return statusFile;
            }
        }

        private DeployResult GetResult(string id, string activeDeploymentId, bool isDeploying)
        {
            var file = VerifyDeployment(id, isDeploying);

            if (file == null)
            {
                return null;
            }

            return new DeployResult
            {
                Id = file.Id,
                Author = file.Author,
                Deployer = file.Deployer,
                AuthorEmail = file.AuthorEmail,
                Message = file.Message,
                Progress = file.Progress,
                StartTime = file.StartTime,
                EndTime = file.EndTime,
                Status = file.Status,
                StatusText = file.StatusText,
                Complete = file.Complete,
                IsTemporary = file.IsTemporary,
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
                throw new ArgumentException("The id parameter is null or empty", "id");
            }

            ILogger logger = null;
            IDeploymentStatusFile currentStatus = null;
            IDisposable buildStep = null;

            try
            {
                logger = GetLogger(id);
                ILogger innerLogger = logger.Log(Resources.Log_PreparingDeployment, TrimId(id));

                currentStatus = _status.Open(id);
                currentStatus.Complete = false;
                currentStatus.StartTime = DateTime.UtcNow;
                currentStatus.Status = DeployStatus.Building;
                currentStatus.StatusText = String.Format(CultureInfo.CurrentCulture, Resources.Status_BuildingAndDeploying, id);
                currentStatus.Save();

                ISiteBuilder builder = null;

                string repositoryRoot = _environment.RepositoryPath;
                var perDeploymentSettings = DeploymentSettingsManager.BuildPerDeploymentSettingsManager(repositoryRoot, _settings);

                try
                {
                    using (tracer.Step("Determining deployment builder"))
                    {
                        builder = _builderFactory.CreateBuilder(tracer, innerLogger, perDeploymentSettings);
                        tracer.Trace("Builder is {0}", builder.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    // If we get a TargetInvocationException, use the inner exception instead to avoid
                    // useless 'Exception has been thrown by the target of an invocation' messages
                    var targetInvocationException = ex as System.Reflection.TargetInvocationException;
                    if (targetInvocationException != null)
                    {
                        ex = targetInvocationException.InnerException;
                    }

                    _globalLogger.Log(ex);

                    tracer.TraceError(ex);

                    innerLogger.Log(ex);

                    currentStatus.MarkFailed();

                    deployStep.Dispose();

                    return;
                }

                buildStep = tracer.Step("Building");

                var context = new DeploymentContext
                {
                    ManifestWriter = GetDeploymentManifestWriter(id),
                    PreviousManifest = GetActiveDeploymentManifestReader(),
                    Tracer = tracer,
                    Logger = logger,
                    GlobalLogger = _globalLogger,
                    OutputPath = GetOutputPath(_environment, perDeploymentSettings),
                };

                context.NextManifestFilePath = context.ManifestWriter.ManifestFilePath;

                if (context.PreviousManifest == null)
                {
                    // In the first deployment we want the wwwroot directory to be cleaned, we do that using a manifest file
                    // That has the expected content of a clean deployment (only one file: hostingstart.html)
                    // This will result in KuduSync cleaning this file.
                    context.PreviousManifest = new DeploymentManifest(Path.Combine(_environment.ScriptPath, Constants.FirstDeploymentManifestFileName));
                }

                context.PreviousManifestFilePath = context.PreviousManifest.ManifestFilePath;

                builder.Build(context)
                       .Then(() =>
                       {
                           // End the build step
                           buildStep.Dispose();

                           TryTouchWebConfig(context);

                           // Run post deployment steps
                           FinishDeployment(id, deployStep);
                       })
                       .Catch(ex =>
                       {
                           // End the build step
                           buildStep.Dispose();

                           currentStatus.MarkFailed();

                           // End the deploy step
                           deployStep.Dispose();

                           return ex.Handled();
                       });
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);

                if (buildStep != null)
                {
                    buildStep.Dispose();
                }

                deployStep.Dispose();
            }
        }

        private static string GetOutputPath(IEnvironment environment, IDeploymentSettingsManager perDeploymentSettings)
        {
            string targetPath = perDeploymentSettings.GetValue(SettingsKeys.TargetPath);

            if (!String.IsNullOrEmpty(targetPath))
            {
                targetPath = targetPath.Trim('\\', '/');
                return Path.GetFullPath(Path.Combine(environment.WebRootPath, targetPath));
            }

            return environment.WebRootPath;
        }

        private IEnumerable<DeployResult> EnumerateResults()
        {
            if (!_fileSystem.Directory.Exists(_environment.DeploymentsPath))
            {
                yield break;
            }

            string activeDeploymentId = _status.ActiveDeploymentId;
            bool isDeploying = IsDeploying;

            foreach (var id in _fileSystem.Directory.GetDirectories(_environment.DeploymentsPath))
            {
                DeployResult result = GetResult(id, activeDeploymentId, isDeploying);

                if (result != null)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Ensure the deployment is in a valid state.
        /// </summary>
        private IDeploymentStatusFile VerifyDeployment(string id, bool isDeploying)
        {
            IDeploymentStatusFile statusFile = _status.Open(id);

            if (statusFile == null)
            {
                return null;
            }

            if (statusFile.Complete)
            {
                return statusFile;
            }

            // There's an incomplete deployment, yet nothing is going on, mark this deployment as failed
            // since it probably means something died
            if (!isDeploying)
            {
                ILogger logger = GetLogger(id);
                logger.LogUnexpetedError();

                statusFile.MarkFailed();
            }

            return statusFile;
        }

        /// <summary>
        /// Runs post deployment steps.
        /// - Marks the active deployment
        /// - Sets the complete flag
        /// </summary>
        private void FinishDeployment(string id, IDisposable deployStep)
        {
            using (deployStep)
            {
                ILogger logger = GetLogger(id);
                logger.Log(Resources.Log_DeploymentSuccessful);

                IDeploymentStatusFile currentStatus = _status.Open(id);
                currentStatus.MarkSuccess();

                _status.ActiveDeploymentId = id;
            }
        }

        private static void TryTouchWebConfig(DeploymentContext context)
        {
            try
            {
                // Touch web.config
                string webConfigPath = Path.Combine(context.OutputPath, "web.config");
                if (File.Exists(webConfigPath))
                {
                    File.SetLastWriteTimeUtc(webConfigPath, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);
            }
        }

        private static string TrimId(string id)
        {
            return id.Substring(0, 10);
        }

        public ILogger GetLogger(string id)
        {
            var path = GetLogPath(id);
            var xmlLogger = new XmlLogger(_fileSystem, path);
            return new ProgressLogger(id, _status, new CascadeLogger(xmlLogger, _globalLogger));
        }

        private IDeploymentManifestWriter GetDeploymentManifestWriter(string id)
        {
            return new DeploymentManifest(GetDeploymentManifestPath(id));
        }

        private IDeploymentManifestReader GetActiveDeploymentManifestReader()
        {
            string id = _status.ActiveDeploymentId;

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
            string path = Path.Combine(_environment.DeploymentsPath, id);

            if (ensureDirectory)
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, path);
            }

            return path;
        }

        private bool IsActive(string id)
        {
            return id.Equals(_status.ActiveDeploymentId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
