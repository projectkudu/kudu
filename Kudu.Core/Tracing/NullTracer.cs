using System;
using System.Collections.Generic;
using System.Diagnostics;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class NullTracer : ITracer
    {
        public static readonly ITracer Instance = new NullTracer();

        private NullTracer()
        {
        }

        public TraceLevel TraceLevel
        {
            get { return TraceLevel.Off; }
        }

        public IDisposable Step(string value, IDictionary<string, string> attributes)
        {
            return DisposableAction.Noop;
        }

        public void Trace(string value, IDictionary<string, string> attributes)
        {

        }
    }
}
