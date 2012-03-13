using System;
using System.Collections.Generic;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class NullTracer : ITracer
    {
        public static ITracer Instance = new NullTracer();

        private NullTracer()
        {
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
