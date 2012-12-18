using Kudu.Contracts.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.ServiceHookHandlers;

namespace Kudu.Services
{
    public class GitFetchHelper
    {
        private readonly IGitServer _gitServer;
        private readonly RepositoryConfiguration _configuration;

        public GitFetchHelper(IGitServer gitServer, RepositoryConfiguration configuration)
        {
            _gitServer = gitServer;
            _configuration = configuration;
        }

        public void Fetch(DeploymentInfo deploymentInfo, string targetBranch)
        {
            // Configure the repository
            _gitServer.Initialize(_configuration);

            // Fetch from url
            _gitServer.FetchWithoutConflict(deploymentInfo.RepositoryUrl, "external", targetBranch);
        }
    }
}
