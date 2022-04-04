using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Helpers;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Core.Deployment
{
    public class FetchDeploymentManager : IFetchDeploymentManager
    {
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeploymentStatusManager _status;
        private readonly string _markerFilePath;

        public FetchDeploymentManager(
            IDeploymentSettingsManager settings,
            IEnvironment environment,
            ITracer tracer,
            IOperationLock deploymentLock,
            IDeploymentManager deploymentManager,
            IDeploymentStatusManager status)
        {
            _settings = settings;
            _environment = environment;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _deploymentManager = deploymentManager;
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

        public async Task<FetchDeploymentRequestResult> FetchDeploy(
            DeploymentInfoBase deployInfo,
            bool asyncRequested,
            Uri requestUri,
            string targetBranch)
        {
            _tracer.Trace($"Starting {nameof(FetchDeploy)}");
            // If Scm is not enabled, we will reject all but one payload for GenericHandler
            // This is to block the unintended CI with Scm providers like GitHub
            // Since Generic payload can only be done by user action, we loosely allow
            // that and assume users know what they are doing.  Same applies to git
            // push/clone endpoint and zip deployment.
            if (!(_settings.IsScmEnabled() || deployInfo.AllowDeploymentWhileScmDisabled))
            {
                return FetchDeploymentRequestResult.ForbiddenScmDisabled;
            }
            // Else if this app is configured with a url in WEBSITE_USE_ZIP, then fail the deployment
            // since this is a RunFromZip site and the deployment has no chance of succeeding.
            else if (_settings.RunFromRemoteZip())
            {
                return FetchDeploymentRequestResult.ConflictRunFromRemoteZipConfigured;
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
                    var successfullyRequested = await PerformBackgroundDeployment(
                        deployInfo,
                        _environment,
                        _settings,
                        _tracer.TraceLevel,
                        requestUri,
                        waitForTempDeploymentCreation);

                    return successfullyRequested
                    ? FetchDeploymentRequestResult.RunningAynschronously
                    : FetchDeploymentRequestResult.ConflictDeploymentInProgress;
                }
            }

            _tracer.Trace("Attempting to fetch target branch {0}", targetBranch);
            try
            {
                return await _deploymentLock.LockOperationAsync(async () =>
                {
                    if (PostDeploymentHelper.IsAutoSwapOngoing())
                    {
                        return FetchDeploymentRequestResult.ConflictAutoSwapOngoing;
                    }

                    await PerformDeployment(deployInfo);
                    return FetchDeploymentRequestResult.RanSynchronously;
                }, "Performing continuous deployment", TimeSpan.Zero);
            }
            catch (LockOperationException)
            {
                if (deployInfo.AllowDeferredDeployment)
                {
                    // Create a marker file that indicates if there's another deployment to pull
                    // because there was a deployment in progress.
                    using (_tracer.Step("Update pending deployment marker file"))
                    {
                        // REVIEW: This makes the assumption that the repository url is the same.
                        // If it isn't the result would be buggy either way.
                        FileSystemHelpers.SetLastWriteTimeUtc(_markerFilePath, DateTime.UtcNow);
                    }

                    return FetchDeploymentRequestResult.Pending;
                }
                else
                {
                    return FetchDeploymentRequestResult.ConflictDeploymentInProgress;
                }
            }
        }

        public async Task PerformDeployment(DeploymentInfoBase deploymentInfo, IDisposable tempDeployment = null, ChangeSet tempChangeSet = null)
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
                    DeployStatusApiResult updateStatusObj = null;

                    try
                    {
                        ILogger logger = _deploymentManager.GetLogger(tempChangeSet.Id, _tracer, deploymentInfo);

                        // Fetch changes from the repository
                        innerLogger = logger.Log(Resources.FetchingChanges);

                        IRepository repository = deploymentInfo.GetRepository();

                        try
                        {
                            await deploymentInfo.Fetch(repository, deploymentInfo, targetBranch, innerLogger, _tracer);
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

                        try
                        {
                            _tracer.Trace($"Before sending {Constants.BuildRequestReceived} status to /api/updatedeploystatus");
                            if (PostDeploymentHelper.IsAzureEnvironment())
                            {
                                if (deploymentInfo != null
                                    && !string.IsNullOrEmpty(deploymentInfo.DeploymentTrackingId))
                                {
                                    // Only send an updatedeploystatus request if DeploymentTrackingId is non null
                                    // This signifies the client has opted in for these deployment updates for this deploy request
                                    updateStatusObj = new DeployStatusApiResult(Constants.BuildRequestReceived, deploymentInfo.DeploymentTrackingId);
                                    bool isSuccess = await _deploymentManager.SendDeployStatusUpdate(updateStatusObj);

                                    if (!isSuccess)
                                    {
                                        // If first operation in itself is unsuccessful,
                                        // set this object to null so subsequent deployment status update operations are not done
                                        updateStatusObj = null;
                                    }
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            _tracer.TraceError($"Exception while sending {Constants.BuildRequestReceived} status to /api/updatedeploystatus. " +
                                $"Entry in the operations table for the deployment status may not have been created. {e}");
                        }

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

                            if (updateStatusObj != null)
                            {
                                updateStatusObj.DeploymentStatus = Constants.BuildInProgress;
                                await _deploymentManager.SendDeployStatusUpdate(updateStatusObj);
                            }

                            await _deploymentManager.DeployAsync(
                                repository,
                                changeSet,
                                deploymentInfo.Deployer,
                                clean: false,
                                deploymentInfo: deploymentInfo,
                                needFileUpdate: deploySpecificCommitId,
                                fullBuildByDefault: deploymentInfo.DoFullBuildByDefault);

                            if (updateStatusObj != null && !deploymentInfo.RestartAllowed)
                            {
                                // If restart is disallowed, send BuildSuccessful here as PostBuildRestartRequired was not sent
                                // during the DeployAsync flow.
                                updateStatusObj.DeploymentStatus = Constants.BuildSuccessful;
                                await _deploymentManager.SendDeployStatusUpdate(updateStatusObj);
                            }
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

                        try
                        {
                            if (updateStatusObj != null)
                            {
                                // Set deployment status as failure if exception is thrown
                                updateStatusObj.DeploymentStatus = Constants.BuildFailed;
                                await _deploymentManager.SendDeployStatusUpdate(updateStatusObj);
                            }
                        }
                        catch
                        {
                            // no-op
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
                    // if last change is not null and finish successfully, mean there was at least one deployment happened
                    // since deployment is now done, trigger swap if enabled
                    await PostDeploymentHelper.PerformAutoSwap(_environment.RequestId, new PostDeploymentTraceListener(_tracer, _deploymentManager.GetLogger(lastChange.Id, _tracer, deploymentInfo)));
                }
            }
        }

        // For continuous integration, we will only build/deploy if fetch new changes
        // The immediate goal is to address duplicated /deploy requests from Bitbucket (retry if taken > 20s)
        private bool ShouldDeploy(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch)
        {
            if (deploymentInfo.IsContinuous)
            {
                ChangeSet changeSet = repository.GetChangeSet(targetBranch);
                return !String.Equals(_status.ActiveDeploymentId, changeSet.Id, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        // key goal is to create background tracer that is independent of request.
        public static async Task<bool> PerformBackgroundDeployment(
            DeploymentInfoBase deployInfo,
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

            // For waiting on creation of temp deployment
            var tempDeploymentCreatedTcs = new TaskCompletionSource<object>();

            // For determining whether or not we failed to create the deployment due to lock contention.
            // Needed for deployments where deferred deployment is not allowed. Will be set to false if
            // lock contention occurs and AllowDeferredDeployment is false, otherwise true.
            var deploymentWillOccurTcs = new TaskCompletionSource<bool>();

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
                    var statusLock = new LockFile(statusLockPath, traceFactory, traceLock: false);
                    var hooksLock = new LockFile(hooksLockPath, traceFactory);
                    var deploymentLock = new DeploymentLockFile(deploymentLockPath, traceFactory);

                    var analytics = new Analytics(settings, new ServerConfiguration(), traceFactory);
                    var deploymentStatusManager = new DeploymentStatusManager(environment, analytics, statusLock);
                    var siteBuilderFactory = new SiteBuilderFactory(new BuildPropertyProvider(), environment);
                    var webHooksManager = new WebHooksManager(tracer, environment, hooksLock);
                    var deploymentManager = new DeploymentManager(siteBuilderFactory, environment, traceFactory, analytics, settings, deploymentStatusManager, deploymentLock, NullLogger.Instance, webHooksManager);
                    var fetchDeploymentManager = new FetchDeploymentManager(settings, environment, tracer, deploymentLock, deploymentManager, deploymentStatusManager);

                    IDisposable tempDeployment = null;

                    try
                    {
                        // Perform deployment
                        deploymentLock.LockOperation(() =>
                        {
                            deploymentWillOccurTcs.TrySetResult(true);

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

                                tempDeploymentCreatedTcs.TrySetResult(null);
                            }

                            fetchDeploymentManager.PerformDeployment(deployInfo, tempDeployment, tempChangeSet).Wait();
                        }, "Performing continuous deployment", TimeSpan.Zero);
                    }
                    catch (LockOperationException)
                    {
                        if (tempDeployment != null)
                        {
                            OperationManager.SafeExecute(() => tempDeployment.Dispose());
                        }

                        if (deployInfo.AllowDeferredDeployment)
                        {
                            deploymentWillOccurTcs.TrySetResult(true);

                            using (tracer.Step("Update pending deployment marker file"))
                            {
                                // REVIEW: This makes the assumption that the repository url is the same.
                                // If it isn't the result would be buggy either way.
                                FileSystemHelpers.SetLastWriteTimeUtc(fetchDeploymentManager._markerFilePath, DateTime.UtcNow);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }
                finally
                {
                    // Will no-op if already set
                    deploymentWillOccurTcs.TrySetResult(false);
                    backgroundTrace.Dispose();
                }
            });

#pragma warning disable 4014
            // Run on BG task (Task.Run) to avoid ASP.NET Request thread terminated with request completion and
            // it doesn't get chance to clean up the pending marker.
            Task.Run(() => PostDeploymentHelper.TrackPendingOperation(deploymentTask, TimeSpan.Zero));
#pragma warning restore 4014

            // When the frontend/ARM calls /deploy with isAsync=true, it starts polling
            // the deployment status immediately, so it's important that the temp deployment
            // is created before we return.
            if (waitForTempDeploymentCreation)
            {
                // deploymentTask may return without creating the temp deployment (lock contention,
                // other exception), in which case just continue.
                await Task.WhenAny(tempDeploymentCreatedTcs.Task, deploymentTask);
            }

            // If deferred deployment is not permitted, we need to know whether or not the deployment was
            // successfully requested. Otherwise, to preserve existing behavior, we assume it was.
            if (!deployInfo.AllowDeferredDeployment)
            {
                return await deploymentWillOccurTcs.Task;
            }
            else
            {
                return true;
            }
        }
    }
}
