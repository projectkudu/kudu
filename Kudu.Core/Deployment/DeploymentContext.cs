using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentContext
    {
        /// <summary>
        /// Path to the previous manifest file.
        /// </summary>
        public string PreviousManifestFilePath { get; set; }

        /// <summary>
        /// Path to the next manifest file.
        /// </summary>
        public string NextManifestFilePath { get; set; }

        public bool IgnoreManifest { get; set; }

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

        /// <summary>
        /// The output path.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// The temporary path that will be provided to the deployment script for build artifacts.
        /// </summary>
        public string BuildTempPath { get; set; }

        /// <summary>
        // Commit ID of the repository to be deployed.
        /// </summary>
        public string CommitId { get; set; }

        /// <summary>
        // Commit message of the repository to be deployed.
        /// </summary>
        public string Message { get; set; }
    }
}
