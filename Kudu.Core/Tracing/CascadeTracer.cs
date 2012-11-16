using System;
using System.Collections.Generic;
using System.Diagnostics;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class CascadeTracer : ITracer
    {
        private readonly ITracer _primary;
        private readonly ITracer _secondary;

        public CascadeTracer(ITracer primary, ITracer secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public TraceLevel TraceLevel
        {
            // both should have the same tracelevel
            get { return _primary.TraceLevel; }
        }

        public IDisposable Step(string message, IDictionary<string, string> attributes)
        {
            IDisposable primary = _primary.Step(message, attributes);
            IDisposable secondary = _secondary.Step(message, attributes);

            return new DisposableAction(() =>
            {
                primary.Dispose();
                secondary.Dispose();
            });
        }

        public void Trace(string message, IDictionary<string, string> attributes)
        {
            _primary.Trace(message, attributes);
            _secondary.Trace(message, attributes);
        }
    }
}
