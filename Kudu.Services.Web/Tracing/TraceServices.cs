using System;
using System.IO;
using System.Web;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;

namespace Kudu.Services.Web.Tracing
{
    public static class TraceServices
    {
        private static readonly object _traceKey = new object();
        private static readonly object _traceFileKey = new object();
        private static readonly object _loggerKey = new object();

        private static Func<ITracer> _traceFactory;
        private static Func<ILogger> _loggerFactory;

        internal static ITracer CurrentRequestTracer
        {
            get
            {
                var httpContext = HttpContext.Current;
                if (httpContext == null)
                {
                    return null;
                }

                return GetRequestTracer(httpContext);
            }
        }

        internal static string CurrentRequestTraceFile
        {
            get
            {
                var httpContext = HttpContext.Current;
                if (httpContext == null)
                {
                    return null;
                }

                return httpContext.Items[_traceFileKey] as string;
            }
        }

        public static void SetTraceFactory(Func<ITracer> traceFactory, Func<ILogger> loggerFactory)
        {
            _traceFactory = traceFactory;
            _loggerFactory = loggerFactory;
        }

        internal static ITracer GetRequestTracer(HttpContext httpContext)
        {
            return httpContext.Items[_traceKey] as ITracer;
        }

        internal static void RemoveRequestTracer(HttpContext httpContext)
        {
            httpContext.Items.Remove(_traceKey);
            httpContext.Items.Remove(_loggerKey);
            httpContext.Items.Remove(_traceFileKey);
        }

        internal static ITracer CreateRequestTracer(HttpContext httpContext)
        {
            var tracer = (ITracer)httpContext.Items[_traceKey];
            if (tracer == null)
            {
                httpContext.Items[_traceFileKey] = String.Format(Constants.TraceFileFormat, Environment.MachineName, Guid.NewGuid().ToString("D"));
                httpContext.Items[_loggerKey] = _loggerFactory();

                tracer = _traceFactory();
                httpContext.Items[_traceKey] = tracer;
            }

            return tracer;
        }
    }
}
