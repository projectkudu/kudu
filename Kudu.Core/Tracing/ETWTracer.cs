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
            // filtering
            // ignore request start, already logged with ApiEvent
            if (message == XmlTracer.IncomingRequestTrace)
            {
                // set the metaData for tracer
                var requestMethod = attributes["method"];
                if (requestMethod == "GET")
                {
                    doTrace = false; // do not log GET
                    return;
                }
                attributes.TryGetValue(Constants.RequestIdHeader, out requestId);
                return;
            }
            // ignore request end, already logged with ApiEvent
            if (message == XmlTracer.OutgoingResponseTrace)
            {
                // clear the metaData for tracer
                doTrace = true;
                requestId = string.Empty;
                return;
            }

            if (doTrace)
            {
                KuduEventSource.Log.GenericEvent(ServerConfiguration.GetApplicationName(),
                                         message,
                                         requestId ?? string.Empty,
                                         string.Empty,
                                         string.Empty,
                                         string.Empty);
            }

        }
    }
}
