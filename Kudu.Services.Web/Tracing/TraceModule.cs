using System;
using System.Web;
using Kudu.Contracts.Tracing;

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
                    httpContext.Request.Path.StartsWith("/profile", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.Path.StartsWith("/diag", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.Path.StartsWith("/elmah.axd", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.RawUrl == "/")
                {
                    return;
                }

                // Setup the request for the tracer
                var tracer = TraceServices.CreateRequesTracer(httpContext);

                httpContext.Items[_stepKey] = tracer.Step(httpContext.Request.RawUrl);
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