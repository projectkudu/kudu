using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Kudu.Contracts.Tracing
{
    public static class TraceExtensions
    {
        public const string AlwaysTrace = "alwaysTrace";

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

            tracer.Trace("Error occurred", attribs);
        }

        public static void TraceError(this ITracer tracer, string message)
        {
            tracer.Trace("Error occurred", new Dictionary<string, string>
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
            return tracer.TraceLevel >= TraceLevel.Verbose || tracer.TraceLevel >= GetTraceLevel(attributes) || attributes.ContainsKey(AlwaysTrace);
        }

        private static TraceLevel GetTraceLevel(IDictionary<string, string> attributes)
        {
            string type;
            attributes.TryGetValue("type", out type);

            if (type == "error")
            {
                return TraceLevel.Error;
            }

            string value;
            if (attributes.TryGetValue("traceLevel", out value))
            {
                var traceLevel = Int32.Parse(value);
                if (traceLevel <= (int)TraceLevel.Error)
                {
                    return TraceLevel.Error;
                }
                else if (traceLevel <= (int)TraceLevel.Info)
                {
                    return TraceLevel.Info;
                }
            }

            return TraceLevel.Verbose;
        }
    }
}
