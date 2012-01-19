using Kudu.Contracts;

namespace Kudu.Core.Deployment
{
    public class DeploymentContext
    {
        public IDeploymentManifestReader PreviousMainfest { get; set; }
        public IDeploymentManifestWriter ManifestWriter { get; set; }
        public IProfiler Profiler { get; set; }
        public ILogger Logger { get; set; }
        public IProgressReporter ProgressReporter { get; set; }
        public string OutputPath { get; set; }
    }
}
