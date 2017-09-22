using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Helpers;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class FetchDeploymentManager
    {
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IDeploymentStatusManager _status;
        private readonly string _markerFilePath;

        public FetchDeploymentManager(
            IDeploymentSettingsManager settings,
            IEnvironment environment,
            ITracer tracer,
            IOperationLock deploymentLock,
            IDeploymentManager deploymentManager,
            IRepositoryFactory repositoryFactory,
            IDeploymentStatusManager status)
        {
            _settings = settings;
            _environment = environment;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _deploymentManager = deploymentManager;
            _repositoryFactory = repositoryFactory;
            _status = status;

            _markerFilePath = Path.Combine(environment.DeploymentsPath, "pending");

            // Prefer marker creation in ctor to delay create when needed.
            // This is to keep the code simple and avoid creation synchronization.
            if (!FileSystemHelpers.FileExists(_markerFilePath))
            {
                try
                {
                    FileSystemHelpers.WriteAllText(_markerFilePath, String.Empty);
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }
            }
        }

        public async Task<DeploymentResponse> DoDeployment(DeploymentInfo deployInfo, bool asyncRequested,
            Uri requestUri, // from UriHelper.GetRequestUri(context.Request)
            string targetBranch
            )
        {
            // If Scm is not enabled, we will reject all but one payload for GenericHandler
            // This is to block the unintended CI with Scm providers like GitHub
            // Since Generic payload can only be done by user action, we loosely allow
            // that and assume users know what they are doing.  Same applies to git
            // push/clone endpoint.
            if (!_settings.IsScmEnabled() && !(deployInfo.Handler is GenericHandler || deployInfo.Handler is DropboxHandler))
            {
                return DeploymentResponse.ForbiddenScmDisabled;
            }

            // for CI payload, we will return Accepted and do the task in the BG
            // if isAsync is defined, we will return Accepted and do the task in the BG
            // since autoSwap relies on the response header, deployment has to be synchronously.
            bool isBackground = asyncRequested || deployInfo.IsContinuous;
            if (isBackground)
            {
                using (_tracer.Step("Start deployment in the background"))
                {
                    var waitForTempDeploymentCreation = asyncRequested;
                    await PerformBackgroundDeployment(
                        deployInfo,
                        _environment,
                        _settings,
                        _tracer.TraceLevel,
                        requestUri,
                        waitForTempDeploymentCreation);
                }

                return DeploymentResponse.RunningInBackground;
            }

            _tracer.Trace("Attempting to fetch target branch {0}", targetBranch);
            try
            {
                return await _deploymentLock.LockOperationAsync(async () =>
                {
                    if (PostDeploymentHelper.IsAutoSwapOngoing())
                    {
                        return DeploymentResponse.AutoSwapOngoing;
                    }

                    await PerformDeployment(deployInfo);
                    return DeploymentResponse.RanSynchronously;
                }, "Performing continuous deployment", TimeSpan.Zero);
            }
            catch (LockOperationException)
            {
                // Create a marker file that indicates if there's another deployment to pull
                // because there was a deployment in progress.
                using (_tracer.Step("Update pending deployment marker file"))
                {
                    // REVIEW: This makes the assumption that the repository url is the same.
                    // If it isn't the result would be buggy either way.
                    FileSystemHelpers.SetLastWriteTimeUtc(_markerFilePath, DateTime.UtcNow);
                }

                return DeploymentResponse.AcceptedAndPending;
            }
        }

        public async Task PerformDeployment(DeploymentInfo deploymentInfo, IDisposable tempDeployment = null, ChangeSet tempChangeSet = null)
        {
            DateTime currentMarkerFileUTC;
            DateTime nextMarkerFileUTC = FileSystemHelpers.GetLastWriteTimeUtc(_markerFilePath);
            ChangeSet lastChange = null;

            do
            {
                // save the current marker
                currentMarkerFileUTC = nextMarkerFileUTC;

                string targetBranch = _settings.GetBranch();

                using (_tracer.Step("Performing fetch based deployment"))
                {
                    // create temporary deployment before the actual deployment item started
                    // this allows portal ui to readily display on-going deployment (not having to wait for fetch to complete).
                    // in addition, it captures any failure that may occur before the actual deployment item started
                    tempDeployment = tempDeployment ?? _deploymentManager.CreateTemporaryDeployment(
                                                    Resources.ReceivingChanges,
                                                    out tempChangeSet,
                                                    deploymentInfo.TargetChangeset,
                                                    deploymentInfo.Deployer);

                    ILogger innerLogger = null;
                    try
                    {
                        ILogger logger = _deploymentManager.GetLogger(tempChangeSet.Id);

                        // Fetch changes from the repository
                        innerLogger = logger.Log(Resources.FetchingChanges);

                        IRepository repository = _repositoryFactory.EnsureRepository(deploymentInfo.RepositoryType);

                        try
                        {
                            await deploymentInfo.Handler.Fetch(repository, deploymentInfo, targetBranch, innerLogger, _tracer);
                        }
                        catch (BranchNotFoundException)
                        {
                            // mark no deployment is needed
                            deploymentInfo.TargetChangeset = null;
                        }

                        // set to null as Deploy() below takes over logging
                        innerLogger = null;

                        // The branch or commit id to deploy
                        string deployBranch = !String.IsNullOrEmpty(deploymentInfo.CommitId) ? deploymentInfo.CommitId : targetBranch;

                        // In case the commit or perhaps fetch do no-op.
                        if (deploymentInfo.TargetChangeset != null && ShouldDeploy(repository, deploymentInfo, deployBranch))
                        {
                            // Perform the actual deployment
                            var changeSet = repository.GetChangeSet(deployBranch);

                            if (changeSet == null && !String.IsNullOrEmpty(deploymentInfo.CommitId))
                            {
                                throw new InvalidOperationException(String.Format("Invalid revision '{0}'!", deploymentInfo.CommitId));
                            }

                            lastChange = changeSet;

                            // Here, we don't need to update the working files, since we know Fetch left them in the correct state
                            // unless for GenericHandler where specific commitId is specified
                            bool deploySpecificCommitId = !String.IsNullOrEmpty(deploymentInfo.CommitId);

                            await _deploymentManager.DeployAsync(repository, changeSet, deploymentInfo.Deployer, clean: false, needFileUpdate: deploySpecificCommitId);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (innerLogger != null)
                        {
                            innerLogger.Log(ex);
                        }

                        // In case the commit or perhaps fetch do no-op.
                        if (deploymentInfo.TargetChangeset != null)
                        {
                            IDeploymentStatusFile statusFile = _status.Open(deploymentInfo.TargetChangeset.Id);
                            if (statusFile != null)
                            {
                                statusFile.MarkFailed();
                            }
                        }

                        throw;
                    }

                    // only clean up temp deployment if successful
                    tempDeployment.Dispose();
                }

                // check marker file and, if changed (meaning new /deploy request), redeploy.
                nextMarkerFileUTC = FileSystemHelpers.GetLastWriteTimeUtc(_markerFilePath);
            } while (deploymentInfo.IsReusable && currentMarkerFileUTC != nextMarkerFileUTC);

            if (lastChange != null && PostDeploymentHelper.IsAutoSwapEnabled())
            {
                IDeploymentStatusFile statusFile = _status.Open(lastChange.Id);
                if (statusFile.Status == DeployStatus.Success)
                {
                    // if last change is not null and finish successfully, mean there was at least one deployoment happened
                    // since deployment is now done, trigger swap if enabled
                    await PostDeploymentHelper.PerformAutoSwap(_environment.RequestId, _environment.SiteRestrictedJwt, new PostDeploymentTraceListener(_tracer, _deploymentManager.GetLogger(lastChange.Id)));
                }
            }
        }

        // For continuous integration, we will only build/deploy if fetch new changes
        // The immediate goal is to address duplicated /deploy requests from Bitbucket (retry if taken > 20s)
        private bool ShouldDeploy(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch)
        {
            if (deploymentInfo.IsContinuous)
            {
                ChangeSet changeSet = repository.GetChangeSet(targetBranch);
                return !String.Equals(_status.ActiveDeploymentId, changeSet.Id, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        // key goal is to create background tracer that is independent of request.
        public static async Task PerformBackgroundDeployment(
            DeploymentInfo deployInfo,
            IEnvironment environment,
            IDeploymentSettingsManager settings,
            TraceLevel traceLevel,
            Uri uri,
            bool waitForTempDeploymentCreation)
        {
            var tracer = traceLevel <= TraceLevel.Off ? NullTracer.Instance : new CascadeTracer(new XmlTracer(environment.TracePath, traceLevel), new ETWTracer(environment.RequestId, "POST"));
            var traceFactory = new TracerFactory(() => tracer);

            var backgroundTrace = tracer.Step(XmlTracer.BackgroundTrace, new Dictionary<string, string>
            {
                {"url", uri.AbsolutePath},
                {"method", "POST"}
            });

            // For monitoring creation of temp deployment
            var tcs = new TaskCompletionSource<object>();

            // This task will be let out of scope intentionally
            var deploymentTask = Task.Run(() =>
            {
                try
                {
                    // lock related
                    string lockPath = Path.Combine(environment.SiteRootPath, Constants.LockPath);
                    string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
                    string statusLockPath = Path.Combine(lockPath, Constants.StatusLockFile);
                    string hooksLockPath = Path.Combine(lockPath, Constants.HooksLockFile);
                    var statusLock = new LockFile(statusLockPath, traceFactory);
                    var hooksLock = new LockFile(hooksLockPath, traceFactory);
                    var deploymentLock = new DeploymentLockFile(deploymentLockPath, traceFactory);

                    var analytics = new Analytics(settings, new ServerConfiguration(), traceFactory);
                    var deploymentStatusManager = new DeploymentStatusManager(environment, analytics, statusLock);
                    var repositoryFactory = new RepositoryFactory(environment, settings, traceFactory);
                    var siteBuilderFactory = new SiteBuilderFactory(new BuildPropertyProvider(), environment);
                    var webHooksManager = new WebHooksManager(tracer, environment, hooksLock);
                    var deploymentManager = new DeploymentManager(siteBuilderFactory, environment, traceFactory, analytics, settings, deploymentStatusManager, deploymentLock, NullLogger.Instance, webHooksManager);
                    var fetchDeploymentManager = new FetchDeploymentManager(settings, environment, tracer, deploymentLock, deploymentManager, repositoryFactory, deploymentStatusManager);

                    IDisposable tempDeployment = null;

                    try
                    {
                        // Perform deployment
                        deploymentLock.LockOperation(() =>
                        {
                            ChangeSet tempChangeSet = null;
                            if (waitForTempDeploymentCreation)
                            {
                                // create temporary deployment before the actual deployment item started
                                // this allows portal ui to readily display on-going deployment (not having to wait for fetch to complete).
                                // in addition, it captures any failure that may occur before the actual deployment item started
                                tempDeployment = deploymentManager.CreateTemporaryDeployment(
                                                                Resources.ReceivingChanges,
                                                                out tempChangeSet,
                                                                deployInfo.TargetChangeset,
                                                                deployInfo.Deployer);

                                tcs.TrySetResult(null);
                            }

                            fetchDeploymentManager.PerformDeployment(deployInfo, tempDeployment, tempChangeSet).Wait();
                        }, "Performing continuous deployment", TimeSpan.Zero);
                    }
                    catch (LockOperationException)
                    {
                        if (tempDeployment != null)
                        {
                            tempDeployment.Dispose();
                        }

                        using (tracer.Step("Update pending deployment marker file"))
                        {
                            // REVIEW: This makes the assumption that the repository url is the same.
                            // If it isn't the result would be buggy either way.
                            FileSystemHelpers.SetLastWriteTimeUtc(fetchDeploymentManager._markerFilePath, DateTime.UtcNow);
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }
                finally
                {
                    backgroundTrace.Dispose();
                }
            });

            // When the frontend/ARM calls /deploy with isAsync=true, it starts polling
            // the deployment status immediately, so it's important that the temp deployment
            // is created before we return.
            if (waitForTempDeploymentCreation)
            {
                // If deploymentTask blows up before it creates the temp deployment,
                // go ahead and return.
                await Task.WhenAny(tcs.Task, deploymentTask);
            }
        }
    }
}
