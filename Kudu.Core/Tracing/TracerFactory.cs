using System;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Tracing
{
    public class TracerFactory : ITraceFactory
    {
        private readonly Func<ITracer> _factory;

        public TracerFactory(Func<ITracer> factory)
        {
            _factory = factory;
        }

        public ITracer GetTracer()
        {
            return _factory();
        }
    }
}
