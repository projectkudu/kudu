using System.Web;
using Kudu.Core.SourceControl.Git;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class GitHubHandler : JsonServiceHookHandler
    {
        public GitHubHandler(IGitServer gitServer)
            : base(gitServer)
        {
        }

        public override bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (request.Headers["X-Github-Event"] == null)
            {
                return false;
            }
            return base.TryGetRepositoryInfo(request, out repositoryInfo);
        }

        protected override string GetDeployer(HttpRequest request)
        {
            return "GitHub";
        }
    }
}