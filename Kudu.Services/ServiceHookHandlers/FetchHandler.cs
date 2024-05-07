using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;
using Kudu.Services.ServiceHookHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Kudu.Core.Deployment;
using Kudu.Core.Settings;

namespace Kudu.Services
{
    public class FetchHandler : HttpTaskAsyncHandler
    {
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnumerable<IServiceHookHandler> _serviceHookHandlers;
        private readonly ITracer _tracer;
        private readonly IFetchDeploymentManager _manager;

        public FetchHandler(ITracer tracer,
                            IDeploymentSettingsManager settings,
                            IFetchDeploymentManager manager,
                            IEnumerable<IServiceHookHandler> serviceHookHandlers)
        {
            _tracer = tracer;
            _settings = settings;
            _serviceHookHandlers = serviceHookHandlers;
            _manager = manager;
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

                DeploymentInfoBase deployInfo = null;

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
                        _tracer.Trace("No-op for deployment.");
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

                bool asyncRequested = String.Equals(context.Request.QueryString["isAsync"], "true", StringComparison.OrdinalIgnoreCase);

                var response = await _manager.FetchDeploy(deployInfo, asyncRequested, context.Request.GetRequestUri(), targetBranch);

                switch (response)
                {
                    case FetchDeploymentRequestResult.RunningAsynchronously:
                        // to avoid regression, only set location header if isAsync
                        if (asyncRequested)
                        {
                            // latest deployment keyword reserved to poll till deployment done
                            context.Response.Headers["Location"] = new Uri(context.Request.GetRequestUri(),
                                String.Format("/api/deployments/{0}?deployer={1}&time={2}", Constants.LatestDeployment, deployInfo.Deployer, DateTime.UtcNow.ToString("yyy-MM-dd_HH-mm-ssZ"))).ToString();
                            context.Response.Headers["Retry-After"] = ScmHostingConfigurations.ArmRetryAfterSeconds.ToString();
                        }
                        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    case FetchDeploymentRequestResult.ForbiddenScmDisabled:
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.ApplicationInstance.CompleteRequest();
                        _tracer.Trace("Scm is not enabled, reject all requests.");
                        return;
                    case FetchDeploymentRequestResult.ConflictAutoSwapOngoing:
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        context.Response.Write(Resources.Error_AutoSwapDeploymentOngoing);
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    case FetchDeploymentRequestResult.Pending:
                        // Return a http 202: the request has been accepted for processing, but the processing has not been completed.
                        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    case FetchDeploymentRequestResult.ConflictDeploymentInProgress:
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        context.Response.Write(Resources.Error_DeploymentInProgress);
                        context.ApplicationInstance.CompleteRequest();
                        break;
                    case FetchDeploymentRequestResult.ConflictRunFromRemoteZipConfigured:
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        context.Response.Write(Resources.Error_RunFromRemoteZipConfigured);
                        context.ApplicationInstance.CompleteRequest();
                        break;
                    case FetchDeploymentRequestResult.RanSynchronously:
                    default:
                        break;
                }
            }
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

        private DeployAction GetRepositoryInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase info)
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
                        info.Fetch = handler.Fetch;
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