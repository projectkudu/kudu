using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Kudu.Contracts.Tracing
{
    public interface ITracer
    {
        TraceLevel TraceLevel { get; }
        IDisposable Step(string message, IDictionary<string, string> attributes);
        void Trace(string message, IDictionary<string, string> attributes);
    }
}
