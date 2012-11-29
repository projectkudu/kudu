using System;
using System.Web;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class GitHubHandler : GitHubCompatHandler
    {
        public GitHubHandler(IGitServer gitServer)
            : base(gitServer)
        {
        }

        public override bool TryGetRepositoryInfo(HttpRequest request, JObject payload, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (request.Headers["X-Github-Event"] != null)
            {
                repositoryInfo = GetRepositoryInfo(request, payload);
                if (repositoryInfo == null)
                {
                    throw new FormatException(Resources.Error_UnsupportedFormat);
                }
            }

            return repositoryInfo != null;
        }

        protected override string GetDeployer(HttpRequest request)
        {
            return "GitHub";
        }
    }
}