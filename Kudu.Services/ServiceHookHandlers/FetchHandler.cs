using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.ServiceHookHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class FetchHandler : HttpTaskAsyncHandler
    {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IDeploymentStatusManager _status;
        private readonly IEnumerable<IServiceHookHandler> _serviceHookHandlers;
        private readonly IOperationLock _deploymentLock;
        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IAutoSwapHandler _autoSwapHandler;
        private readonly string _markerFilePath;

        public FetchHandler(ITracer tracer,
                            IDeploymentManager deploymentManager,
                            IDeploymentSettingsManager settings,
                            IDeploymentStatusManager status,
                            IOperationLock deploymentLock,
                            IEnvironment environment,
                            IEnumerable<IServiceHookHandler> serviceHookHandlers,
                            IRepositoryFactory repositoryFactory,
                            IAutoSwapHandler autoSwapHandler)
        {
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _environment = environment;
            _deploymentManager = deploymentManager;
            _settings = settings;
            _status = status;
            _serviceHookHandlers = serviceHookHandlers;
            _repositoryFactory = repositoryFactory;
            _autoSwapHandler = autoSwapHandler;
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

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            using (_tracer.Step("FetchHandler"))
            {
                // Redirect GET /deploy requests to the Kudu root for convenience when using URL from Azure portal
                if (String.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("~/");
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                if (!String.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                context.Response.TrySkipIisCustomErrors = true;

                DeploymentInfo deployInfo = null;

                // We are going to assume that the branch details are already set by the time it gets here. This is particularly important in the mercurial case,
                // since Settings hardcodes the default value for Branch to be "master". Consequently, Kudu will NoOp requests for Mercurial commits.
                string targetBranch = _settings.GetBranch();
                try
                {
                    var request = new HttpRequestWrapper(context.Request);
                    JObject payload = GetPayload(request);
                    DeployAction action = GetRepositoryInfo(request, payload, targetBranch, out deployInfo);
                    if (action == DeployAction.NoOp)
                    {
                        return;
                    }

                    // If Scm is not enabled, we will reject all but one payload for GenericHandler
                    // This is to block the unintended CI with Scm providers like GitHub
                    // Since Generic payload can only be done by user action, we loosely allow
                    // that and assume users know what they are doing.  Same applies to git
                    // push/clone endpoint.
                    if (!_settings.IsScmEnabled() && !(deployInfo.Handler is GenericHandler || deployInfo.Handler is DropboxHandler))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    }
                }
                catch (FormatException ex)
                {
                    _tracer.TraceError(ex);
                    context.Response.StatusCode = 400;
                    context.Response.Write(ex.Message);
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                // for CI payload, we will return Accepted and do the task in the BG
                // if isAsync is defined, we will return Accepted and do the task in the BG
                // since autoSwap relies on the response header, deployment has to be synchronously.
                bool isAsync = String.Equals(context.Request.QueryString["isAsync"], "true", StringComparison.OrdinalIgnoreCase);
                bool isBackground = isAsync || deployInfo.IsContinuous;
                if (isBackground)
                {
                    using (_tracer.Step("Start deployment in the background"))
                    {
                        ChangeSet tempChangeSet = null;
                        IDisposable tempDeployment = null;
                        if (isAsync)
                        {
                            // create temporary deployment before the actual deployment item started
                            // this allows portal ui to readily display on-going deployment (not having to wait for fetch to complete).
                            // in addition, it captures any failure that may occur before the actual deployment item started
                            tempDeployment = _deploymentManager.CreateTemporaryDeployment(
                                                            Resources.ReceivingChanges,
                                                            out tempChangeSet,
                                                            deployInfo.TargetChangeset,
                                                            deployInfo.Deployer);
                        }

                        PerformBackgroundDeployment(deployInfo, _environment, _settings, _tracer.TraceLevel, context.Request.Url, tempDeployment, _autoSwapHandler, tempChangeSet);
                    }

                    // to avoid regression, only set location header if isAsync
                    if (isAsync)
                    {
                        // latest deployment keyword reserved to poll till deployment done
                        context.Response.Headers["Location"] = new Uri(context.Request.Url,
                            String.Format("/api/deployments/{0}?deployer={1}&time={2}", Constants.LatestDeployment, deployInfo.Deployer, DateTime.UtcNow.ToString("yyy-MM-dd_HH-mm-ssZ"))).ToString();
                    }
                    context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                _tracer.Trace("Attempting to fetch target branch {0}", targetBranch);
                bool acquired = await _deploymentLock.TryLockOperationAsync(async () =>
                {
                    if (_autoSwapHandler.IsAutoSwapOngoing())
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        context.Response.Write(Resources.Error_AutoSwapDeploymentOngoing);
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    }

                    await PerformDeployment(deployInfo);
                }, TimeSpan.Zero);

                if (!acquired)
                {
                    // Create a marker file that indicates if there's another deployment to pull
                    // because there was a deployment in progress.
                    using (_tracer.Step("Update pending deployment marker file"))
                    {
                        // REVIEW: This makes the assumption that the repository url is the same.
                        // If it isn't the result would be buggy either way.
                        FileSystemHelpers.SetLastWriteTimeUtc(_markerFilePath, DateTime.UtcNow);
                    }

                    // Return a http 202: the request has been accepted for processing, but the processing has not been completed.
                    context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                    context.ApplicationInstance.CompleteRequest();
                }
            }
        }


        public async Task PerformDeployment(DeploymentInfo deploymentInfo, IDisposable tempDeployment = null, ChangeSet tempChangeSet = null)
        {
            DateTime currentMarkerFileUTC;
            DateTime nextMarkerFileUTC = FileSystemHelpers.GetLastWriteTimeUtc(_markerFilePath);

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
                            await deploymentInfo.Handler.Fetch(repository, deploymentInfo, targetBranch, innerLogger);
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

        private void TracePayload(JObject json)
        {
            var attribs = new Dictionary<string, string>
            {
                { "json", json.ToString() }
            };

            _tracer.Trace("payload", attribs);
        }

        private void TraceHandler(IServiceHookHandler handler)
        {
            var attribs = new Dictionary<string, string>
            {
                { "type", handler.GetType().FullName }
            };

            _tracer.Trace("handler", attribs);
        }

        private DeployAction GetRepositoryInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo info)
        {
            foreach (var handler in _serviceHookHandlers)
            {
                DeployAction result = handler.TryParseDeploymentInfo(request, payload, targetBranch, out info);
                if (result != DeployAction.UnknownPayload)
                {
                    if (_tracer.TraceLevel >= TraceLevel.Verbose)
                    {
                        TraceHandler(handler);
                    }

                    if (result == DeployAction.ProcessDeployment)
                    {
                        // Although a payload may be intended for a handler, it might not need to fetch.
                        // For instance, if a different branch was pushed than the one the repository is deploying, we can no-op it.
                        Debug.Assert(info != null);
                        info.Handler = handler;
                    }

                    return result;
                }
            }

            throw new FormatException(Resources.Error_UnsupportedFormat);
        }

        private JObject GetPayload(HttpRequestBase request)
        {
            JObject payload;

            // we don't care about content type, just let it choked
            if (request.Form.Count > 0)
            {
                string json = request.Form["payload"];
                if (String.IsNullOrEmpty(json))
                {
                    json = request.Form[0];
                }

                payload = JsonConvert.DeserializeObject<JObject>(json);
            }
            else
            {
                using (JsonTextReader reader = new JsonTextReader(new StreamReader(request.GetInputStream())))
                {
                    payload = JObject.Load(reader);
                }
            }

            if (payload == null)
            {
                throw new FormatException(Resources.Error_EmptyPayload);
            }

            if (_tracer.TraceLevel >= TraceLevel.Verbose)
            {
                TracePayload(payload);
            }

            return payload;
        }

        // key goal is to create background tracer that is independent of request.
        public static void PerformBackgroundDeployment(DeploymentInfo deployInfo, IEnvironment environment, IDeploymentSettingsManager settings, TraceLevel traceLevel, Uri uri, IDisposable tempDeployment, IAutoSwapHandler autoSwapHandler, ChangeSet tempChangeSet)
        {
            var tracer = traceLevel <= TraceLevel.Off ? NullTracer.Instance : new XmlTracer(environment.TracePath, traceLevel);
            var traceFactory = new TracerFactory(() => tracer);

            var backgroundTrace = tracer.Step(XmlTracer.BackgroundTrace, new Dictionary<string, string>
            {
                {"url", uri.AbsolutePath},
                {"method", "POST"}
            });

            Task.Run(() =>
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
                    var deploymentManager = new DeploymentManager(siteBuilderFactory, environment, traceFactory, analytics, settings, deploymentStatusManager, deploymentLock, NullLogger.Instance, webHooksManager, autoSwapHandler);
                    var fetchHandler = new FetchHandler(tracer, deploymentManager, settings, deploymentStatusManager, deploymentLock, environment, null, repositoryFactory, null);

                    // Perform deployment
                    var acquired = deploymentLock.TryLockOperation(() =>
                    {
                        fetchHandler.PerformDeployment(deployInfo, tempDeployment, tempChangeSet).Wait();
                    }, TimeSpan.Zero);

                    if (!acquired)
                    {
                        if (tempDeployment != null)
                        {
                            tempDeployment.Dispose();
                        }

                        using (tracer.Step("Update pending deployment marker file"))
                        {
                            // REVIEW: This makes the assumption that the repository url is the same.
                            // If it isn't the result would be buggy either way.
                            FileSystemHelpers.SetLastWriteTimeUtc(fetchHandler._markerFilePath, DateTime.UtcNow);
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
        }
    }
}