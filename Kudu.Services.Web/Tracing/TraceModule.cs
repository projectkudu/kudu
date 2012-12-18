using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using Kudu.Contracts.Tracing;

namespace Kudu.Services.Web.Tracing
{
    public class TraceModule : IHttpModule
    {
        private static readonly object _stepKey = new object();

        public void Init(HttpApplication app)
        {
            app.BeginRequest += (sender, e) =>
            {
                var httpContext = ((HttpApplication)sender).Context;

                // Skip certain paths
                if (httpContext.Request.RawUrl.EndsWith("favicon.ico", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.Path.StartsWith("/dump", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.RawUrl == "/")
                {
                    return;
                }

                // Setup the request for the tracer
                var tracer = TraceServices.CreateRequestTracer(httpContext);

                if (tracer == null || tracer.TraceLevel <= TraceLevel.Off)
                {
                    return;
                }

                var attribs = new Dictionary<string, string>
                {
                    { "url", httpContext.Request.RawUrl },
                    { "method", httpContext.Request.HttpMethod },
                    { "type", "request" }
                };

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

                AddTraceLevel(httpContext, attribs);

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

        private void AddTraceLevel(HttpContext httpContext, Dictionary<string, string> attribs)
        {
            if (!httpContext.Request.RawUrl.StartsWith("/logstream", StringComparison.OrdinalIgnoreCase) &&
                !httpContext.Request.RawUrl.StartsWith("/deployments", StringComparison.OrdinalIgnoreCase))
            {
                attribs["traceLevel"] = ((int)TraceLevel.Info).ToString();
            }
        }

        public void Dispose() { }
    }
}