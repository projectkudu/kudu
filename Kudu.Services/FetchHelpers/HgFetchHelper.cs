using Kudu.Core;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.ServiceHookHandlers;

namespace Kudu.Services
{
    public class HgFetchHelper
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;

        public HgFetchHelper(IEnvironment environment, ITraceFactory traceFactory)
        {
            _environment = environment;
        }

        public void Fetch(DeploymentInfo deploymentInfo, string targetBranch)
        {
            var hgRepository = new HgRepository(_environment.RepositoryPath, _traceFactory);
            // Attempt to create a hg repository if it does not already exist.
            hgRepository.Initialize();
            
            hgRepository.FetchWithoutConflict(deploymentInfo.RepositoryUrl, null, targetBranch);
        }
    }
}
