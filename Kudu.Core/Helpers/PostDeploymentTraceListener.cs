using System;
using System.Diagnostics;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Tracing;

namespace Kudu.Core.Helpers
{
    public class PostDeploymentTraceListener : TraceListener
    {
        private readonly ILogger _logger;
        private readonly ITracer _tracer;

        public PostDeploymentTraceListener(ITracer tracer, ILogger logger = null)
        {
            _logger = logger;
            _tracer = tracer;
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (_logger != null && eventType <= TraceEventType.Information)
            {
                _logger.Log(format, args);
            }

            _tracer.Trace(format, args);
        }

        public override void Write(string message)
        {
            // not used
            throw new NotImplementedException();
        }

        public override void WriteLine(string message)
        {
            // not used
            throw new NotImplementedException();
        }
    }
}