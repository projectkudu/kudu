using System;
using System.Collections.Generic;
using System.Web;

namespace Kudu.Services.Web.Tracing
{
    public class TraceModule : IHttpModule
    {
        private static readonly object _stepKey = new object();

        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, e) =>
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
                var tracer = TraceServices.CreateRequesTracer(httpContext);

                httpContext.Items[_stepKey] = tracer.Step("Incoming Request", new Dictionary<string, string>
                {
                    { "url", httpContext.Request.RawUrl },
                    { "method", httpContext.Request.HttpMethod },
                    { "type", "request" }
                });
            };

            context.EndRequest += (sender, e) =>
            {
                var httpContext = ((HttpApplication)sender).Context;

                var requestStep = (IDisposable)httpContext.Items[_stepKey];

                if (requestStep != null)
                {
                    requestStep.Dispose();
                }
            };
        }

        public void Dispose() { }
    }
}