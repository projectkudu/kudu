using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Kudu.Contracts.Tracing
{
    public static class TraceExtensions
    {
        private static readonly Dictionary<string, string> _empty = new Dictionary<string, string>();

        public static IDisposable Step(this ITracer tracer, string message)
        {
            return tracer.Step(message, _empty);
        }

        public static void Trace(this ITracer tracer, string message, params object[] args)
        {
            tracer.Trace(String.Format(message, args), _empty);
        }

        public static void TraceError(this ITracer tracer, Exception ex)
        {
            var attribs = new Dictionary<string, string>
            {
                { "type", "error" },
                { "text", ex.Message },
                { "stackTrace", ex.StackTrace ?? String.Empty }
            };

            if (ex.InnerException != null)
            {
                attribs["innerText"] = ex.InnerException.Message;
                attribs["innerStackTrace"] = ex.InnerException.StackTrace ?? String.Empty;
            }

            tracer.Trace("Error occured", attribs);
        }

        public static void TraceError(this ITracer tracer, string message)
        {
            tracer.Trace("Error occured", new Dictionary<string, string>
            {
                { "type", "error" },
                { "text", message }
            });
        }

        public static void TraceWarning(this ITracer tracer, string message, params object[] args)
        {
            tracer.Trace("Warning", new Dictionary<string, string>
            {
                { "type", "warning" },
                { "text", String.Format(message, args) }
            });
        }

        public static bool ShouldTrace(this ITracer tracer, IDictionary<string, string> attributes)
        {
            return tracer.TraceLevel >= TraceLevel.Verbose || tracer.TraceLevel >= tracer.GetTraceLevel(attributes);
        }

        public static TraceLevel GetTraceLevel(this ITracer tracer, IDictionary<string, string> attributes)
        {
            string type;
            attributes.TryGetValue("type", out type);

            if (IsError(type, attributes))
            {
                return TraceLevel.Error;
            }
            else if (IsInfo(type, attributes))
            {
                return TraceLevel.Info;
            }
            else
            {
                return TraceLevel.Verbose;
            }
        }

        private static bool IsError(string type, IDictionary<string, string> attributes)
        {
            if (type == "error")
            {
                return true;
            }

            string value;
            if (attributes.TryGetValue("traceLevel", out value))
            {
                return Int32.Parse(value) <= (int)TraceLevel.Error;
            }

            return false;
        }

        // we don't include "error" in info as caller must be checking for that already
        private static bool IsInfo(string type, IDictionary<string, string> attributes)
        {
            string value;
            if (attributes.TryGetValue("traceLevel", out value))
            {
                return Int32.Parse(value) <= (int)TraceLevel.Info;
            }

            return false;
        }
    }
}
