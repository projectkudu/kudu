using System;
using System.Collections.Generic;

namespace Kudu.Contracts.Tracing
{
    public interface ITracer
    {
        IDisposable Step(string value, IDictionary<string, string> attributes);
        void Trace(string value, IDictionary<string, string> attributes);
    }
}
