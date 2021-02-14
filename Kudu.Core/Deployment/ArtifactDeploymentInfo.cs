using System.IO;
using System.Net.Http;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment
{
    public class ArtifactDeploymentInfo : DeploymentInfoBase
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;

        public ArtifactDeploymentInfo(IEnvironment environment, ITraceFactory traceFactory)
        {
            _environment = environment;
            _traceFactory = traceFactory;
        }

        public override IRepository GetRepository()
        {
            // Artifact "repository" does not conflict with other types, including NoRepository,
            // so there's no call to EnsureRepository
            var path = Path.Combine(_environment.ZipTempPath, Constants.ArtifactStagingDirectoryName);
            return new NullRepository(path, _traceFactory, DeploymentTrackingId);
        }

        public string Author { get; set; }

        public string AuthorEmail { get; set; }

        public string Message { get; set; }

        // Optional file name. Used by certain features like run-from-zip.
        public string ArtifactFileName { get; set; }

        // This is used when getting the artifact file from the remote URL
        public string RemoteURL { get; set; }
    }
}
