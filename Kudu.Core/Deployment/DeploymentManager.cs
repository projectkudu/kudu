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
                return EnumerateResults().ToList();
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

                _fileSystem.Directory.Delete(path, true);
            }
        }

        public void Deploy(IRepository repository, ChangeSet changeSet, string deployer, bool clean)
        {
            ITracer tracer = _traceFactory.GetTracer();
            IDisposable deployStep = null;
            ILogger innerLogger = null;
            var deploymentRepository = new DeploymentRepository(repository);

            // If we don't get a changeset, find out what branch we should be deploying and update the repo to it
            if (changeSet == null)
            {
                string targetBranch = _settings.GetBranch();
                deploymentRepository.Update(targetBranch);

                changeSet = deploymentRepository.GetChangeSet(repository.CurrentId);
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

                using (tracer.Step("Updating to specific changeset"))
                {
                    innerLogger = logger.Log(Resources.Log_UpdatingBranch, id);

                    // Update to the the specific changeset
                    deploymentRepository.Update(id);
                }

                using (tracer.Step("Updating submodules"))
                {
                    innerLogger = logger.Log(Resources.Log_UpdatingSubmodules);
                    
                    deploymentRepository.UpdateSubmodules();
                }

                if (clean)
                {
                    tracer.Trace("Cleaning {0} repository", repository.RepositoryType);

                    innerLogger = logger.Log(Resources.Log_CleaningRepository, repository.RepositoryType);

                    deploymentRepository.Clean();
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

        public void Deploy(IRepository repository, string deployer)
        {
            var tracer = _traceFactory.GetTracer();
            IDisposable deployStep = null;
            ILogger innerLogger = null;
            var deploymentRepository = new DeploymentRepository(repository);
            IDeploymentStatusFile statusFile = null;

            try
            {
                deployStep = tracer.Step("Deploy");
                ReceiveInfo receiveInfo = deploymentRepository.GetReceiveInfo();

                string targetBranch = _settings.GetBranch();

                tracer.Trace("Deploying branch '{0}'", targetBranch);

                // Something went wrong here since we weren't able to deploy if receiveInfo is null
                if (receiveInfo == null || !targetBranch.Equals(receiveInfo.Branch.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (receiveInfo == null)
                    {
                        tracer.TraceWarning("Push info was null. Post receive hook didn't execute correctly");
                    }
                    else
                    {
                        tracer.Trace("Unexpected branch deployed '{0}'.", receiveInfo.Branch.Name);

                        _globalLogger.Log(Resources.Log_UnexpectedBranchPushed, receiveInfo.Branch.Name, targetBranch);
                    }

                    deployStep.Dispose();
                    return;
                }

                // Get the pushed branch's id
                string id = receiveInfo.Branch.Id;
                // If nothing changed then do nothing
                if (IsActive(id))
                {
                    tracer.Trace("Deployment '{0}' already active", id);

                    _globalLogger.Log(Resources.Log_DeploymentAlreadyActive, id);

                    deployStep.Dispose();
                    return;
                }

                ChangeSet changeSet = deploymentRepository.GetChangeSet(id);
                statusFile = GetOrCreateStatusFile(changeSet, tracer, deployer);
                statusFile.MarkPending();

                ILogger logger = GetLogger(changeSet.Id);
                innerLogger = logger.Log(Resources.Log_NewDeploymentReceived);

                using (tracer.Step("Update to " + receiveInfo.Branch.Name))
                {
                    innerLogger = logger.Log(Resources.Log_UpdatingBranch, receiveInfo.Branch.Name);

                    using (var progressWriter = new ProgressWriter())
                    {
                        progressWriter.Start();

                        // Update to the target branch
                        deploymentRepository.Update(targetBranch);
                    }
                }

                using (tracer.Step("Update submodules"))
                {
                    innerLogger = logger.Log(Resources.Log_UpdatingSubmodules);

                    using (var progressWriter = new ProgressWriter())
                    {
                        progressWriter.Start();

                        deploymentRepository.UpdateSubmodules();
                    }
                }

                // set to null as Build() below takes over logging
                innerLogger = null;

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

                _globalLogger.Log(ex);

                tracer.TraceError(ex);

                if (deployStep != null)
                {
                    deployStep.Dispose();
                }
            }
        }

        public IDisposable CreateTemporaryDeployment(string statusText, ChangeSet changeSet = null, string deployedBy = null)
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

            // Return a handle that deletes the deployment on dispose.
            return new DisposableAction(() =>
            {
                if (changeSet.IsTemporary)
                {
                    DeleteTemporaryDeployment(changeSet.Id);
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

        private IDeploymentStatusFile GetOrCreateStatusFile(ChangeSet changeSet, ITracer tracer, string deployer)
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
                    statusFile.Message = changeSet.Message;
                    statusFile.Author = changeSet.AuthorName;
                    statusFile.Deployer = deployer;
                    statusFile.AuthorEmail = changeSet.AuthorEmail;
                    statusFile.Save();
                }

                return statusFile;
            }
        }

        /// <summary>
        /// Deletes the temporary deployment, will not fail if it doesn't exist.
        /// </summary>
        private void DeleteTemporaryDeployment(string id)
        {
            string temporaryDeploymentPath = GetRoot(id, ensureDirectory: false);
            FileSystemHelpers.DeleteDirectorySafe(temporaryDeploymentPath);
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
                currentStatus.StartTime = DateTime.Now;
                currentStatus.Status = DeployStatus.Building;
                currentStatus.StatusText = String.Format(CultureInfo.CurrentCulture, Resources.Status_BuildingAndDeploying, id);
                currentStatus.Save();

                ISiteBuilder builder = null;

                try
                {
                    builder = _builderFactory.CreateBuilder(tracer, innerLogger);
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
                    OutputPath = _environment.WebRootPath,
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

        private IEnumerable<DeployResult> EnumerateResults()
        {
            if (!_fileSystem.Directory.Exists(_environment.DeploymentCachePath))
            {
                yield break;
            }

            string activeDeploymentId = _status.ActiveDeploymentId;
            bool isDeploying = IsDeploying;

            foreach (var id in _fileSystem.Directory.GetDirectories(_environment.DeploymentCachePath))
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

        public void CleanWwwRoot()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("Cleaning wwwroot"))
            {
                string activeDeploymentId = _status.ActiveDeploymentId;

                // Clean only required if there is an actual deployment (active deployment id is not null)
                if (activeDeploymentId != null)
                {
                    string emptyTempPath = Path.Combine(_environment.TempPath, Guid.NewGuid().ToString());

                    try
                    {
                        // Create an empty temporary directory
                        FileSystemHelpers.EnsureDirectory(emptyTempPath);

                        string from = emptyTempPath;
                        string to = _environment.WebRootPath;
                        string previousManifest = GetDeploymentManifestPath(activeDeploymentId);
                        string nextManifest = Path.Combine(emptyTempPath, "manifest");

                        // Run kudu sync from the empty directory to wwwroot using the current manifest
                        // This will cause all previously deployed files to be removed from wwwroot
                        var exe = new Executable(Path.Combine(_environment.ScriptPath, "kudusync.cmd"), emptyTempPath, _settings.GetCommandIdleTimeout());
                        Tuple<string, string> result = exe.Execute(tracer, "-f {0} -t {1} -p {2} -n {3} -v 50", from, to, previousManifest, nextManifest);

                        if (!String.IsNullOrEmpty(result.Item1))
                        {
                            tracer.Trace("Process output: {0}", result.Item1);
                        }
                        if (!String.IsNullOrEmpty(result.Item2))
                        {
                            tracer.Trace("Process error: {0}", result.Item2);
                        }
                    }
                    finally
                    {
                        FileSystemHelpers.DeleteDirectorySafe(emptyTempPath);
                    }
                }
                else
                {
                    tracer.Trace("No active deployment to clean");
                }
            }
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
            string path = Path.Combine(_environment.DeploymentCachePath, id);

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
