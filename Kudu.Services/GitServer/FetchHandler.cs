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
using Kudu.Core.SourceControl.Git;
using Kudu.Services.GitServer.ServiceHookHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer
{
    public class FetchHandler : GitServerHttpHandler
    {
        private readonly IDeploymentSettingsManager _settings;
        private readonly RepositoryConfiguration _configuration;
        private readonly IEnvironment _environment;
        private readonly IEnumerable<IServiceHookHandler> _serviceHookHandlers;

        public FetchHandler(ITracer tracer,
                            IGitServer gitServer,
                            IDeploymentManager deploymentManager,
                            IDeploymentSettingsManager settings,
                            IOperationLock deploymentLock,
                            RepositoryConfiguration configuration,
                            IEnvironment environment,
                            IEnumerable<IServiceHookHandler> serviceHookHandlers)
            : base(tracer, gitServer, deploymentLock, deploymentManager)
        {
            _settings = settings;
            _configuration = configuration;
            _environment = environment;
            _serviceHookHandlers = serviceHookHandlers;
        }

        private string MarkerFilePath
        {
            get
            {
                return Path.Combine(_environment.DeploymentCachePath, "pending");
            }
        }

        public override void ProcessRequest(HttpContext context)
        {
            using (_tracer.Step("FetchHandler"))
            {
                context.Response.TrySkipIisCustomErrors = true;

                RepositoryInfo repositoryInfo = null;
                try
                {
                    JObject payload = GetPayload(context.Request);
                    repositoryInfo = GetRepositoryInfo(context.Request, payload);
                }
                catch (FormatException ex)
                {
                    _tracer.TraceError(ex);
                    context.Response.StatusCode = 400;
                    context.Response.Write(ex.Message);
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                string targetBranch = _settings.GetValue(SettingsKeys.Branch);

                _tracer.Trace("Attempting to fetch target branch {0}", targetBranch);

                _deploymentLock.LockOperation(() =>
                {
                    PerformDeployment(repositoryInfo, targetBranch);
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

        private void PerformDeployment(RepositoryInfo repositoryInfo, string targetBranch)
        {
            bool hasPendingDeployment;

            do
            {
                hasPendingDeployment = false;

                using (_tracer.Step("Performing fetch based deployment"))
                {
                    using (_deploymentManager.CreateTemporaryDeployment(Resources.FetchingChanges))
                    {
                        // Configure the repository
                        _gitServer.Initialize(_configuration);

                        // Setup the receive info (this is important to know if branches were deleted etc)
                        _gitServer.SetReceiveInfo(repositoryInfo.OldRef, repositoryInfo.NewRef, targetBranch);

                        // Fetch from url
                        repositoryInfo.Handler.Fetch(repositoryInfo, targetBranch);

                        // Perform the actual deployment
                        _deploymentManager.Deploy(repositoryInfo.Deployer);

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

        private RepositoryInfo GetRepositoryInfo(HttpRequest request, JObject payload)
        {
            RepositoryInfo info = null;
            foreach (var handler in _serviceHookHandlers)
            {
                if (handler.TryGetRepositoryInfo(request, payload, out info))
                {
                    if (_tracer.TraceLevel >= TraceLevel.Verbose)
                    {
                        TraceHandler(handler);
                    }

                    info.Handler = handler;
                    return info;
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
