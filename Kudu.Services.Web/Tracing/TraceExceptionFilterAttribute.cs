using System;
using System.Diagnostics;
using System.Web.Http.Filters;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Services.Web.Tracing
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public sealed class TraceExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            ITracer tracer = TraceServices.CurrentRequestTracer;

            if (tracer == null || tracer.TraceLevel <= TraceLevel.Off)
            {
                return;
            }

            tracer.TraceError(context.Exception);
        }
    }
}