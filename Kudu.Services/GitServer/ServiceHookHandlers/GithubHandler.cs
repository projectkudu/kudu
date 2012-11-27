using System.Web;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class GithubHandler : JsonServiceHookHandler
    {
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