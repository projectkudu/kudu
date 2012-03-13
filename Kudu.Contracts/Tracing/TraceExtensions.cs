using System;
using System.Collections.Generic;

namespace Kudu.Contracts.Tracing
{
    public static class TraceExtensions
    {
        private static readonly Dictionary<string, string> _empty = new Dictionary<string, string>();

        public static IDisposable Step(this ITracer tracer, string value)
        {
            return tracer.Step(value, _empty);
        }

        public static void Trace(this ITracer tracer, string value, params object[] args)
        {
            tracer.Trace(String.Format(value, args), _empty);
        }

        public static void TraceError(this ITracer tracer, Exception ex)
        {
            tracer.Trace(ex.Message, new Dictionary<string, string>
            {
                { "type", "error" },
                { "stack.trace", ex.StackTrace }
            });
        }

        public static void TraceWarning(this ITracer tracer, string value, params object[] args)
        {
            tracer.Trace(String.Format(value, args), new Dictionary<string, string>
            {
                { "type", "warning" }
            });
        }
    }
}
