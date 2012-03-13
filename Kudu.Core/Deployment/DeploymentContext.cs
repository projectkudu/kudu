using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentContext
    {
        public IDeploymentManifestReader PreviousMainfest { get; set; }
        public IDeploymentManifestWriter ManifestWriter { get; set; }
        public ITracer Tracer { get; set; }
        public ILogger Logger { get; set; }
        public string OutputPath { get; set; }
    }
}
