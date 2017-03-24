using System;
using System.Collections.Generic;
using System.Diagnostics;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class ETWTracer : ITracer
    {
        private string requestId = string.Empty;
        private bool doTrace = true;
        private bool lookForRequestBegin = true;

        public ETWTracer(string requestId)
        {
            // a new ETWTracer per request
            this.requestId = requestId;
        }

        // TODO traceLevel does NOT YET apply to ETWTracer
        public TraceLevel TraceLevel
        {
            get
            {
                return TraceLevel.Verbose;
            }
        }

        public IDisposable Step(string message, IDictionary<string, string> attributes)
        {
            if (doTrace)
            {
                Trace(message, attributes);
            }

            return DisposableAction.Noop;
        }

        public void Trace(string message, IDictionary<string, string> attributes)
        {
            if (lookForRequestBegin && message == XmlTracer.IncomingRequestTrace)
            {
                lookForRequestBegin = false;

                var requestMethod = attributes["method"];
                if (requestMethod == "GET") //TODO still trace if there's error
                {
                    doTrace = false; // under no circumstances, we log GET (even if lvl==verbose)
                    return;
                }
                // ignore request start, already logged with ApiEvent
                return;
            }

            if (message == XmlTracer.OutgoingResponseTrace)
            {
                doTrace = true;
                lookForRequestBegin = true;
                requestId = string.Empty;
                // ignore request end, already logged with ApiEvent
                return;
            }

            if (doTrace)
            {
                KuduEventSource.Log.GenericEvent(ServerConfiguration.GetApplicationName(),
                                         message,
                                         requestId,
                                         string.Empty,
                                         string.Empty,
                                         string.Empty);
            }

        }
    }
}
