using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Web;
using Kudu.Contracts.Tracing;

namespace Kudu.Services.Web.Tracing
{
    public class TraceModule : IHttpModule
    {
        private static readonly object _stepKey = new object();
        private static int _traceStartup;

        public void Init(HttpApplication app)
        {
            app.BeginRequest += (sender, e) =>
            {
                var httpContext = ((HttpApplication)sender).Context;

                var tracer = TraceStartup(httpContext);

                // Skip certain paths
                if (httpContext.Request.RawUrl.EndsWith("favicon.ico", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.Path.StartsWith("/dump", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.RawUrl == "/")
                {
                    return;
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
                    if (!key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        attribs["h_" + key] = httpContext.Request.Headers[key];
                    }
                }

                if (httpContext.Request.RawUrl.Contains(".git") ||
                    httpContext.Request.RawUrl.EndsWith("/deploy", StringComparison.OrdinalIgnoreCase))
                {
                    // Mark git requests specially
                    attribs.Add("git", "true");
                }

                httpContext.Items[_stepKey] = tracer.Step("Incoming Request", attribs);
            };

            app.Error += (sender, e) =>
            {
                try
                {
                    var httpContext = ((HttpApplication)sender).Context;
                    var tracer = TraceServices.GetRequestTracer(httpContext);

                    if (tracer == null || tracer.TraceLevel <= TraceLevel.Off)
                    {
                        return;
                    }

                    tracer.TraceError(app.Server.GetLastError());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            };

            app.EndRequest += (sender, e) =>
            {
                var httpContext = ((HttpApplication)sender).Context;
                var tracer = TraceServices.GetRequestTracer(httpContext);

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
                    attribs["traceLevel"] = ((int)TraceLevel.Error).ToString();
                }
                else
                {
                    AddTraceLevel(httpContext, attribs);
                }

                foreach (string key in httpContext.Response.Headers)
                {
                    attribs["h_" + key] = httpContext.Response.Headers[key];
                }

                tracer.Trace("Outgoing response", attribs);

                var requestStep = (IDisposable)httpContext.Items[_stepKey];

                if (requestStep != null)
                {
                    requestStep.Dispose();
                }
            };
        }

        private static void AddTraceLevel(HttpContext httpContext, Dictionary<string, string> attribs)
        {
            if (!httpContext.Request.RawUrl.StartsWith("/logstream", StringComparison.OrdinalIgnoreCase) &&
                !httpContext.Request.RawUrl.StartsWith("/deployments", StringComparison.OrdinalIgnoreCase))
            {
                attribs["traceLevel"] = ((int)TraceLevel.Info).ToString();
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

                    tracer.Trace("Startup Request", attribs);
                }
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
            attribs.Add("pid", String.Format("{0},{1},{2}",
                Process.GetCurrentProcess().Id,
                AppDomain.CurrentDomain.Id.ToString(),
                System.Threading.Thread.CurrentThread.ManagedThreadId));

            return attribs;
        }

        public void Dispose() { }
    }
}