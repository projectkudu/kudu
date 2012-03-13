using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public interface ISiteBuilderFactory
    {
        ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger);
    }
}
