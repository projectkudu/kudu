using System;
using System.Net.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.Web.Tracing
{
    public class TraceErrorHandler : HttpErrorHandler
    {
        private readonly ITraceFactory _traceFactory;

        public TraceErrorHandler(ITraceFactory traceFactory)
        {
            _traceFactory = traceFactory;
        }

        protected override bool OnTryProvideResponse(Exception exception, ref HttpResponseMessage message)
        {
            ITracer tracer = _traceFactory.GetTracer();

            tracer.TraceError(exception);

            return false;
        }
    }
}