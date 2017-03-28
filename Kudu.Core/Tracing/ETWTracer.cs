using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class ETWTracer : ITracer
    {
        // log REST api
        private string _requestId;
        private bool _lookForRequestBegin;
        private bool _lookForRequestEnd;
        private string _requestMethod;

        public ETWTracer(string requestId, string requestMethod = "")
        {
            // a new ETWTracer per request
            this._requestId = requestId;
            if (string.IsNullOrEmpty(requestMethod))
            {
                this._lookForRequestBegin = true; // expecting a "Incoming Request" to start logging
                this._lookForRequestEnd = true; // expecting a "Outgoing Response" to finish logging

                this._requestMethod = string.Empty;
            }
            else
            {
                this._lookForRequestBegin = false; // skip the if clause, start logging rightaway
                this._lookForRequestEnd = false; // skip the if clause, start logging rightaway

                this._requestMethod = requestMethod;
            }

        }

        // traceLevel does NOT YET apply to ETWTracer
        public TraceLevel TraceLevel
        {
            get
            {
                return TraceLevel.Verbose;
            }
        }

        public IDisposable Step(string message, IDictionary<string, string> attributes)
        {
            Trace(message, attributes);

            return DisposableAction.Noop;
        }

        public void Trace(string message, IDictionary<string, string> attributes)
        {
            if (_lookForRequestBegin)
            {
                if (message == XmlTracer.IncomingRequestTrace)
                {
                    _lookForRequestBegin = false; // start logging from next trace()
                    _requestMethod = attributes["method"];
                }
                // ignore if its not incoming request (ie TraceShutDown())
                return;
            }
            else
            {
                if (_lookForRequestEnd && message == XmlTracer.OutgoingResponseTrace)
                {
                    //TODO, if this instance is not reused, no need to reset the state
                    _lookForRequestEnd = false;
                    _lookForRequestBegin = true;
                    _requestId = string.Empty;
                    _requestMethod = string.Empty;
                    // ignore request end, already logged with ApiEvent
                    return;
                }

                if (_requestMethod == "GET")
                {
                    // ignore normal GET request body
                    var type = string.Empty;
                    attributes.TryGetValue("type", out type);
                    if (string.IsNullOrEmpty(type) || (type != "error" && type != "warning")) //TODO enum
                    {
                        return;
                    }
                }

                var strb = new StringBuilder();
                strb.Append(message + " ");
                // took from XMLtracer
                foreach (var attrib in attributes)
                {
                    if (TraceExtensions.IsNonDisplayableAttribute(attrib.Key))
                    {
                        continue;
                    }

                    strb.AppendFormat("{0}=\"{1}\" ", attrib.Key, attrib.Value);
                }

                KuduEventSource.Log.GenericEvent(ServerConfiguration.GetApplicationName(),
                                             strb.ToString(),
                                             _requestId,
                                             string.Empty,
                                             string.Empty,
                                             string.Empty);
            }
        }
    }
}
