using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
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
        private readonly ITracer _tracer;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IFileSystem _fileSystem;
        private readonly string _markerFilePath;

        public FetchHandler(ITracer tracer,
                            IDeploymentManager deploymentManager,
                            IDeploymentSettingsManager settings,
                            IDeploymentStatusManager status,
                            IOperationLock deploymentLock,
                            IEnvironment environment,
                            IEnumerable<IServiceHookHandler> serviceHookHandlers,
                            IRepositoryFactory repositoryFactory,
                            IFileSystem fileSystem)
        {
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _deploymentManager = deploymentManager;
            _settings = settings;
            _status = status;
            _serviceHookHandlers = serviceHookHandlers;
            _repositoryFactory = repositoryFactory;
            _fileSystem = fileSystem;
            _markerFilePath = Path.Combine(environment.DeploymentsPath, "pending");

            // Prefer marker creation in ctor to delay create when needed.
            // This is to keep the code simple and avoid creation synchronization.
            if (!_fileSystem.File.Exists(_markerFilePath))
            {
                try
                {
                    _fileSystem.File.WriteAllText(_markerFilePath, String.Empty);
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

                _tracer.Trace("Attempting to fetch target branch {0}", targetBranch);
                bool acquired = await _deploymentLock.TryLockOperationAsync(async () =>
                {
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
                        _fileSystem.File.SetLastWriteTimeUtc(_markerFilePath, DateTime.UtcNow);
                    }

                    // Return a http 202: the request has been accepted for processing, but the processing has not been completed.
                    context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                    context.ApplicationInstance.CompleteRequest();
                }
            }
        }

        public async Task PerformDeployment(DeploymentInfo deploymentInfo)
        {
            DateTime currentMarkerFileUTC;
            DateTime nextMarkerFileUTC = _fileSystem.File.GetLastWriteTimeUtc(_markerFilePath);

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
                    ChangeSet tempChangeSet;
                    IDisposable tempDeployment = _deploymentManager.CreateTemporaryDeployment(
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

                        // In case the commit or perhaps fetch do no-op.
                        if (deploymentInfo.TargetChangeset != null && ShouldDeploy(repository, deploymentInfo, targetBranch))
                        {
                            // Perform the actual deployment
                            var changeSet = repository.GetChangeSet(targetBranch);

                            // Here, we don't need to update the working files, since we know Fetch left them in the correct state
                            await _deploymentManager.DeployAsync(repository, changeSet, deploymentInfo.Deployer, clean: false, needFileUpdate: false);
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
                nextMarkerFileUTC = _fileSystem.File.GetLastWriteTimeUtc(_markerFilePath);

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
    }
}
