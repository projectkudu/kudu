using Kudu.Contracts.Tracing;

namespace Kudu.Core.Tracing
{
    public interface ITraceFactory
    {
        ITracer GetTracer();
    }
}
