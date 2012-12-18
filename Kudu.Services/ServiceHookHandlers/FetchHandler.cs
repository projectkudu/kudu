using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Kudu.Services.ServiceHookHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class FetchHandler : IHttpHandler
    {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly IEnumerable<IServiceHookHandler> _serviceHookHandlers;
        private readonly IOperationLock _deploymentLock;
        private readonly ITracer _tracer;
        private readonly RepositoryConfiguration _configuration;


        public FetchHandler(ITracer tracer,
                            IDeploymentManager deploymentManager,
                            IDeploymentSettingsManager settings,
                            IOperationLock deploymentLock,
                            RepositoryConfiguration configuration,
                            IEnvironment environment,
                            IEnumerable<IServiceHookHandler> serviceHookHandlers)
        {
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _deploymentManager = deploymentManager;
            _settings = settings;
            _configuration = configuration;
            _environment = environment;
            _serviceHookHandlers = serviceHookHandlers;
        }

        public bool IsReusable
        {
            get { return false; }
        }

        private string MarkerFilePath
        {
            get
            {
                return Path.Combine(_environment.DeploymentCachePath, "pending");
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            Debugger.Break();
            Debugger.Launch();
            using (_tracer.Step("FetchHandler"))
            {
                context.Response.TrySkipIisCustomErrors = true;

                DeploymentInfo deployInfo = null;
                
                // We are going to assume that the branch details are already set by the time it gets here. This is particularly important in the mercurial case, 
                // since Settings hardcodes the default value for Branch to be "master". Consequently, Kudu will NoOp requests for Mercurial commits.
                string targetBranch = _settings.GetValue(SettingsKeys.Branch);
                try
                {
                    JObject payload = GetPayload(context.Request);
                    DeployAction action = GetRepositoryInfo(context.Request, payload, targetBranch, out deployInfo);
                    if (action == DeployAction.NoOp)
                    {
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
                _deploymentLock.LockOperation(() =>
                {
                    PerformDeployment(deployInfo, targetBranch);
                },
                () =>
                {
                    // Create a marker file that indicates if there's another deployment to pull
                    // because there was a deployment in progress.
                    using (_tracer.Step("Creating pending deployment maker file"))
                    {
                        // REVIEW: This makes the assumption that the repository url is the same.
                        // If it isn't the result would be buggy either way.
                        CreateMarkerFile();
                    }

                    context.Response.StatusCode = 409;
                    context.ApplicationInstance.CompleteRequest();
                });
            }
        }

        private void CreateMarkerFile()
        {
            File.WriteAllText(MarkerFilePath, String.Empty);
        }

        private bool MarkerFileExists()
        {
            return File.Exists(MarkerFilePath);
        }

        private bool DeleteMarkerFile()
        {
            return FileSystemHelpers.DeleteFileSafe(MarkerFilePath);
        }

        private void PerformDeployment(DeploymentInfo deploymentInfo, string targetBranch)
        {
            bool hasPendingDeployment;

            do
            {
                hasPendingDeployment = false;

                var handler = deploymentInfo.Handler;
                using (_tracer.Step("Performing fetch based deployment"))
                {
                    _tracer.Trace("Creating deployment file for changeset {0}", deploymentInfo.TargetChangeset.Id);
                    _deploymentManager.CreateDeployment(deploymentInfo.TargetChangeset, deploymentInfo.Deployer, Resources.FetchingChanges);
                    
                    // Fetch changes from the repository
                    deploymentInfo.Handler.Fetch(deploymentInfo, targetBranch);
                    
                    // Perform the actual deployment
                    _deploymentManager.Deploy(deploymentInfo.TargetChangeset.Id, deploymentInfo.Deployer, clean: false);

                    if (MarkerFileExists())
                    {
                        _tracer.Trace("Pending deployment marker file exists");

                        hasPendingDeployment = DeleteMarkerFile();

                        if (hasPendingDeployment)
                        {
                            _tracer.Trace("Deleted marker file");
                        }
                        else
                        {
                            _tracer.TraceError("Failed to delete marker file");
                        }
                    }
                }

            } while (hasPendingDeployment);
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

        private DeployAction GetRepositoryInfo(HttpRequest request, JObject payload, string targetBranch, out DeploymentInfo info)
        {
            var httpRequestBase = new HttpRequestWrapper(request);
            foreach (var handler in _serviceHookHandlers)
            {
                DeployAction result = handler.TryParseDeploymentInfo(httpRequestBase, payload, targetBranch, out info);
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

        private JObject GetPayload(HttpRequest request)
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
