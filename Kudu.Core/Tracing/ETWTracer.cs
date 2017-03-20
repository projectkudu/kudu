using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class ETWTracer : ITracer
    {
        private string requestId = string.Empty;
        private bool doTrace = true;
        private bool lookForRequestBegin = true;

        // TODO traceLevel does not apply to ETWTracer
        public TraceLevel TraceLevel
        {
            get
            {
                throw new NotImplementedException();
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
                if (requestMethod == "GET")
                {
                    doTrace = false; // do not log GET
                    return;
                }
                // attributes.TryGetValue(Constants.RequestIdHeader, out requestId); // out could be null
                requestId = attributes["requestId"]; // set in logBeginRequest, could be x-arr-log-id, x-ms-request-id or new GUI()
                // requestId = System.Environment.GetEnvironmentVariable(Constants.RequestIdHeader); // empty
                // ignore request start, already logged with ApiEvent
                return;
            }

            if (message == XmlTracer.OutgoingResponseTrace)
            {
                doTrace = true;
                lookForRequestBegin = true;
                requestId = string.Empty;
                // ignore request end, already logged with ApiEvent
                return; // do not log request end
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
