using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.DynamicData;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;
using Kudu.Services.Web.Infrastruture;

namespace Kudu.Services.Web.Tracing
{
    public class TraceModule : IHttpModule
    {
        private static readonly DateTime _startDateTime = DateTime.UtcNow;
        private static readonly object _stepKey = new object();
        private static int _traceStartup;
        private static DateTime _lastRequestDateTime;

        // (/|$) means either "/" or end-of-line
        // {0,2} means repeat pattern 0 to 2 times
        private static Regex[] _rbacWhiteListPaths = new[]
        {
            new Regex(@"^/api/siteextensions(/|$)", RegexOptions.IgnoreCase),
            new Regex(@"^/api/functions((/|$)([^/]*|$)){0,2}(/|$)$", RegexOptions.IgnoreCase),
            new Regex(@"^/api/deployments((/|$)([^/]*|$)){0,2}(/|$)$", RegexOptions.IgnoreCase),
            new Regex(@"^/api/(processes|webjobs|triggeredwebjobs|continuouswebjobs)((/|$)([^/]*|$)){0,1}(/|$)$", RegexOptions.IgnoreCase),
        };

        // list of paths returning potentially sensitive data
        private static readonly string[] DisallowedPaths = new string[]
        {
            "/api/functions/admin/masterkey",
            "/api/functions/admin/token"
        };

        public static TimeSpan UpTime
        {
            get { return DateTime.UtcNow - _startDateTime; }
        }

        public static TimeSpan LastRequestTime
        {
            get { return DateTime.UtcNow - _lastRequestDateTime; }
        }

        public void Init(HttpApplication app)
        {
            app.BeginRequest += OnBeginRequest;
            app.Error += OnError;
            app.EndRequest += OnEndRequest;
        }

        private static void OnBeginRequest(object sender, EventArgs e)
        {
            _lastRequestDateTime = DateTime.UtcNow;

            var httpContext = ((HttpApplication)sender).Context;
            var httpRequest = new HttpRequestWrapper(httpContext.Request);

            LogBeginRequest(httpContext);

            // HACK: This is abusing the trace module
            // Disallow GET requests from CSM extensions bridge
            // Except if owner or coadmin (aka legacy or non-rbac) or x-ms-client-rolebased-contributor (by FE) authorization
            if (!String.IsNullOrEmpty(httpRequest.Headers["X-MS-VIA-EXTENSIONS-ROUTE"]) &&
                httpRequest.HttpMethod.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(httpRequest.Headers[Constants.ClientAuthorizationSourceHeader], "legacy", StringComparison.OrdinalIgnoreCase) &&
                httpRequest.Headers[Constants.RoleBasedContributorHeader] != "1" &&
                !IsRbacWhiteListPaths(httpRequest.Url.AbsolutePath))
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                httpContext.Response.End();
            }

            TryConvertSpecialHeadersToEnvironmentVariable(httpRequest);

            // HACK: If it's a Razor extension, add a dummy extension to prevent WebPages for blocking it,
            // as we need to serve those files via /vfs
            // Yes, this is an abuse of the trace module
            if (httpRequest.FilePath.IndexOf("vfs/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (httpRequest.FilePath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                httpRequest.FilePath.EndsWith(".vbhtml", StringComparison.OrdinalIgnoreCase)))
            {
                httpContext.Server.TransferRequest(httpRequest.FilePath + Constants.DummyRazorExtension, preserveForm: true);
                return;
            }

            // Always trace the startup request.
            ITracer tracer = TraceStartup(httpContext);

            // Skip certain paths
            if (TraceExtensions.ShouldSkipRequest(httpRequest))
            {
                // this is to prevent Kudu being IFrame (typically where host != referer)
                // to optimize where we return X-FRAME-OPTIONS DENY header, only return when 
                // in Azure env, browser non-ajax requests and referer mismatch with host
                // since browser uses referer for other scenarios (such as href, redirect), we may return 
                // this header (benign) in such cases.
                if (Kudu.Core.Environment.IsAzureEnvironment() && !TraceExtensions.IsAjaxRequest(httpRequest) && TraceExtensions.MismatchedHostReferer(httpRequest))
                {
                    httpContext.Response.Headers.Add("X-FRAME-OPTIONS", "DENY");
                }

                if (TraceServices.TraceLevel != TraceLevel.Verbose)
                {
                    TraceServices.RemoveRequestTracer(httpContext);

                    // enable just ETW tracer
                    tracer = TraceServices.EnsureETWTracer(httpContext);
                }
            }

            tracer = tracer ?? TraceServices.CreateRequestTracer(httpContext);

            if (tracer == null || tracer.TraceLevel <= TraceLevel.Off)
            {
                return;
            }

            var attribs = GetTraceAttributes(httpContext);

            AddTraceLevel(httpContext, attribs);

            foreach (string key in httpContext.Request.Headers)
            {
                if (!key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("X-MS-CLIENT-PRINCIPAL-NAME", StringComparison.OrdinalIgnoreCase))
                {
                    attribs[key] = httpContext.Request.Headers[key];
                }
                else
                {
                    // for sensitive header, we only trace first 3 characters following by "..."
                    var value = httpContext.Request.Headers[key];
                    attribs[key] = string.IsNullOrEmpty(value) ? value : (value.Substring(0, Math.Min(3, value.Length)) + "...");
                }
            }

            httpContext.Items[_stepKey] = tracer.Step(XmlTracer.IncomingRequestTrace, attribs);
        }

        public static bool IsRbacWhiteListPaths(string path)
        {
            path = path.ToLower();
            return !DisallowedPaths.Any(p => path.Contains(p.ToLower())) && _rbacWhiteListPaths.Any(r => r.IsMatch(path));
        }

        private static void OnEndRequest(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            var tracer = TraceServices.GetRequestTracer(httpContext);

            LogEndRequest(httpContext);

            if (tracer == null || tracer.TraceLevel <= TraceLevel.Off)
            {
                return;
            }

            var attribs = new Dictionary<string, string>
                {
                    { "type", "response" },
                    { "statusCode", httpContext.Response.StatusCode.ToString() },
                    { "statusText", httpContext.Response.StatusDescription }
                };

            if (httpContext.Response.StatusCode >= 400)
            {
                attribs[TraceExtensions.TraceLevelKey] = ((int)TraceLevel.Error).ToString();
            }
            else
            {
                AddTraceLevel(httpContext, attribs);
            }

            // Response.Headers is not supported in Classic mode, so just skip this
            if (HttpRuntime.UsingIntegratedPipeline)
            {
                foreach (string key in httpContext.Response.Headers)
                {
                    attribs[key] = httpContext.Response.Headers[key];
                }
            }

            tracer.Trace(XmlTracer.OutgoingResponseTrace, attribs);

            var requestStep = (IDisposable)httpContext.Items[_stepKey];

            if (requestStep != null)
            {
                requestStep.Dispose();
            }
        }

        private static void OnError(object sender, EventArgs e)
        {
            try
            {
                HttpApplication app = (HttpApplication)sender;
                var httpContext = app.Context;
                var tracer = TraceServices.GetRequestTracer(httpContext);
                var error = app.Server.GetLastError();

                LogErrorRequest(httpContext, error);

                if (tracer == null || tracer.TraceLevel <= TraceLevel.Off)
                {
                    return;
                }

                tracer.TraceError(error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private static void LogBeginRequest(HttpContext httpContext)
        {
            OperationManager.SafeExecute(() =>
            {
                var request = httpContext.Request;
                var requestId = request.GetRequestId() ?? Guid.NewGuid().ToString();
                httpContext.Items[Constants.RequestIdHeader] = requestId;
                httpContext.Items[Constants.RequestDateTimeUtc] = DateTime.UtcNow;
                KuduEventSource.Log.ApiEvent(
                    ServerConfiguration.GetRuntimeSiteName(),
                    "OnBeginRequest",
                    request.RawUrl,
                    request.HttpMethod,
                    requestId,
                    0,
                    0,
                    request.GetUserAgent());
            });
        }

        private static void LogEndRequest(HttpContext httpContext)
        {
            OperationManager.SafeExecute(() =>
            {
                var request = httpContext.Request;
                var response = httpContext.Response;
                var requestId = (string)httpContext.Items[Constants.RequestIdHeader];
                var requestTime = (DateTime)httpContext.Items[Constants.RequestDateTimeUtc];
                var latencyInMilliseconds = (long)(DateTime.UtcNow - requestTime).TotalMilliseconds;
                KuduEventSource.Log.ApiEvent(
                    ServerConfiguration.GetRuntimeSiteName(),
                    "OnEndRequest",
                    request.RawUrl,
                    request.HttpMethod,
                    requestId,
                    response.StatusCode,
                    latencyInMilliseconds,
                    request.GetUserAgent());
            });
        }

        private static void LogErrorRequest(HttpContext httpContext, Exception ex)
        {
            OperationManager.SafeExecute(() =>
            {
                var request = httpContext.Request;
                var response = httpContext.Response;
                var requestId = (string)httpContext.Items[Constants.RequestIdHeader];
                var requestTime = (DateTime)httpContext.Items[Constants.RequestDateTimeUtc];
                var latencyInMilliseconds = (long)(DateTime.UtcNow - requestTime).TotalMilliseconds;
                KuduEventSource.Log.ApiEvent(
                    ServerConfiguration.GetRuntimeSiteName(),
                    $"OnErrorRequest {ex}",
                    request.RawUrl,
                    request.HttpMethod,
                    requestId,
                    response.StatusCode,
                    latencyInMilliseconds,
                    request.GetUserAgent());
            });
        }

        private static void AddTraceLevel(HttpContext httpContext, Dictionary<string, string> attribs)
        {
            if (!httpContext.Request.RawUrl.StartsWith("/logstream", StringComparison.OrdinalIgnoreCase) &&
                !httpContext.Request.RawUrl.StartsWith("/deployments", StringComparison.OrdinalIgnoreCase))
            {
                attribs[TraceExtensions.TraceLevelKey] = ((int)TraceLevel.Info).ToString();
            }
        }

        private static ITracer TraceStartup(HttpContext httpContext)
        {
            ITracer tracer = null;

            // 0 means this is the very first request starting up Kudu
            if (0 == Interlocked.Exchange(ref _traceStartup, 1))
            {
                tracer = TraceServices.CreateRequestTracer(httpContext);

                if (tracer != null && tracer.TraceLevel > TraceLevel.Off)
                {
                    var attribs = GetTraceAttributes(httpContext);

                    // force always trace
                    attribs[TraceExtensions.AlwaysTrace] = "1";

                    // Dump environment variables
                    foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                    {
                        var key = (string)entry.Key;
                        if (key.StartsWith("SCM", StringComparison.OrdinalIgnoreCase))
                        {
                            attribs[key] = (string)entry.Value;
                        }
                    }

                    tracer.Trace(XmlTracer.StartupRequestTrace, attribs);
                }

                OperationManager.SafeExecute(() =>
                {
                    var requestId = (string)httpContext.Items[Constants.RequestIdHeader];
                    KuduEventSource.Log.GenericEvent(
                        ServerConfiguration.GetRuntimeSiteName(),
                        string.Format("StartupRequest pid:{0}, domain:{1}, UseSiteExtensionV1:{2}", Process.GetCurrentProcess().Id, AppDomain.CurrentDomain.Id, DeploymentSettingsExtension.UseSiteExtensionV1.Value),
                        requestId,
                        Environment.GetEnvironmentVariable(SettingsKeys.ScmType),
                        Environment.GetEnvironmentVariable(SettingsKeys.WebSiteSku),
                        EnvironmentHelper.KuduVersion.Value,
                        EnvironmentHelper.AppServiceVersion.Value);
                });
            }

            return tracer;
        }

        private static Dictionary<string, string> GetTraceAttributes(HttpContext httpContext)
        {
            var attribs = new Dictionary<string, string>
                {
                    { "url", httpContext.Request.RawUrl },
                    { "method", httpContext.Request.HttpMethod },
                    { "type", "request" }
                };

            // Add an attribute containing the process, AppDomain and Thread ids to help debugging
            attribs.Add("pid", String.Join(",",
                Process.GetCurrentProcess().Id,
                AppDomain.CurrentDomain.Id.ToString(),
                System.Threading.Thread.CurrentThread.ManagedThreadId));

            return attribs;
        }

        private static void TryConvertSpecialHeadersToEnvironmentVariable(HttpRequestWrapper request)
        {
            try
            {
                // RDBug 6738223 : AlwaysOn request again SCM has wrong Host Name to main site
                // Ignore Always on request for now till bug is fixed
                if (!string.Equals("AlwaysOn", request.UserAgent, StringComparison.OrdinalIgnoreCase))
                {
                    System.Environment.SetEnvironmentVariable(Constants.HttpHost, request.Url.Host);
                    System.Environment.SetEnvironmentVariable(Constants.HttpAuthority, request.Url.Authority);
                }
            }
            catch
            {
                // this is temporary hack for host name invalid due to ~ (http://~1hostname/)
                // we don't know how to repro it yet.
            }
        }

        public void Dispose()
        {
        }
    }
}