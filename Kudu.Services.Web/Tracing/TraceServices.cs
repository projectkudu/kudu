using System;
using System.Web;
using Kudu.Contracts.Tracing;

namespace Kudu.Services.Web.Tracing
{
    public static class TraceServices
    {
        private static readonly object _traceKey = new object();
        private static Func<ITracer> _traceFactory;

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

        public static void SetTraceFactory(Func<ITracer> traceFactory)
        {
            _traceFactory = traceFactory;
        }

        internal static ITracer GetRequestTracer(HttpContext httpContext)
        {
            return httpContext.Items[_traceKey] as ITracer;
        }

        internal static ITracer CreateRequesTracer(HttpContext httpContext)
        {
            var tracer = (ITracer)httpContext.Items[_traceKey];
            if (tracer == null)
            {
                tracer = _traceFactory();
                httpContext.Items[_traceKey] = tracer;
            }

            return tracer;
        }
    }
}

