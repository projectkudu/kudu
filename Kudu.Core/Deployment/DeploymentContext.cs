using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentContext
    {
        public IDeploymentManifestReader PreviousMainfest { get; set; }
        public IDeploymentManifestWriter ManifestWriter { get; set; }

        /// <summary>
        /// Path to the previous manifest file.
        /// </summary>
        public string PreviousManifestFilePath { get; set; }

        /// <summary>
        /// Path to the next manifest file.
        /// </summary>
        public string NextManifestFilePath { get; set; }

        /// <summary>
        /// Writes diagnostic output to the trace.
        /// </summary>
        public ITracer Tracer { get; set; }

        /// <summary>
        /// The logger for the current operation (building, copying files etc.)
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// The logger for the entire deployment operation.
        /// </summary>
        public ILogger GlobalLogger { get; set; }

        public string OutputPath { get; set; }
    }
}
