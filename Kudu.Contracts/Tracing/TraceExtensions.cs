using System;
using System.Collections.Generic;

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
    }
}
