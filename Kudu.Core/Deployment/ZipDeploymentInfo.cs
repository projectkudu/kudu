using System;
using Kudu.Core.SourceControl;
using System.IO;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment
{
    public class ZipDeploymentInfo : DeploymentInfoBase
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;

        public ZipDeploymentInfo(IEnvironment environment, ITraceFactory traceFactory)
        {
            _environment = environment;
            _traceFactory = traceFactory;
        }

        public override IRepository GetRepository()
        {
            // Zip "repository" does not conflict with other types, including NoRepository,
            // so there's no call to EnsureRepository
            var path = Path.Combine(_environment.ZipTempPath, Constants.ZipExtractPath);
            return new NullRepository(path, _traceFactory);
        }

        public string Author { get; set; }

        public string AuthorEmail { get; set; }

        public string Message { get; set; }
    }
}
