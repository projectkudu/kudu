using System;
using System.Collections.Generic;

namespace Kudu.Contracts.Tracing
{
    public interface ITracer
    {
        IDisposable Step(string message, IDictionary<string, string> attributes);
        void Trace(string message, IDictionary<string, string> attributes);
    }
}
