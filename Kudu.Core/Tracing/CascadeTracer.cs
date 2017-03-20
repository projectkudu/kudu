using System;
using System.Collections.Generic;
using System.Diagnostics;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class CascadeTracer : ITracer
    {
        private readonly ITracer[] tracers;

        public CascadeTracer(params ITracer[] tracers)
        {
            if (tracers.Length == 0)
            {
                throw new ArgumentException("Requires at least one tracer.");
            }

            this.tracers = tracers;
        }

        public TraceLevel TraceLevel
        {
            // all should have the same tracelevel
            get { return tracers[0].TraceLevel; }
        }

        public IDisposable Step(string message, IDictionary<string, string> attributes)
        {
            IDisposable[] finishSteps = new IDisposable[tracers.Length];
            for (int i = 0; i < tracers.Length; i++)
            {
                finishSteps[i] = tracers[i].Step(message, attributes);
            }

            return new DisposableAction(() =>
            {
                foreach (IDisposable finishStep in finishSteps)
                {
                    finishStep.Dispose();
                }
            });
        }

        public void Trace(string message, IDictionary<string, string> attributes)
        {
            foreach (ITracer tracer in tracers)
            {
                tracer.Trace(message, attributes);
            }
        }
    }
}
