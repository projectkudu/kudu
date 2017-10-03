using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment
{
    public interface ISiteBuilderFactory
    {
        ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger, IDeploymentSettingsManager settings, IRepository fileFinder);
    }
}
