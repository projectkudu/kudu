using System.Web;
using Kudu.Contracts.SourceControl;
using Kudu.Core;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public abstract class PolymorphicHandler : IServiceHookHandler
    {
        private readonly IGitServer _gitServer;
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _tracer;
        private readonly RepositoryConfiguration _repositoryConfiguration;

        public PolymorphicHandler(IGitServer gitServer, ITraceFactory tracer, IEnvironment environment, RepositoryConfiguration  repositoryConfiguration)
        {
            _gitServer = gitServer;
            _repositoryConfiguration = repositoryConfiguration;
            _environment = environment;
            _tracer = tracer;
        }

        public abstract DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo);

        public void Fetch(DeploymentInfo deploymentInfo, string targetBranch)
        {
            if (deploymentInfo.RepositoryType == Core.SourceControl.RepositoryType.Git)
            {
                var gitFetchHelper = new GitFetchHelper(_gitServer, _repositoryConfiguration);
                gitFetchHelper.Fetch(deploymentInfo, targetBranch);
            }
            else
            {
                var hgFetchHelper = new HgFetchHelper(_environment, _tracer);
                hgFetchHelper.Fetch(deploymentInfo, targetBranch);
            }
        }
    }
}
