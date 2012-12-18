using System;
using System.Web;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class CodebaseHqHandler : GitHubCompatHandler
    {
        public CodebaseHqHandler(IGitServer gitServer)
            : base(gitServer)
        {
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith("Codebasehq", StringComparison.OrdinalIgnoreCase))
            {
                deploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                return deploymentInfo == null ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        protected override DeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            var info = base.GetDeploymentInfo(request, payload, targetBranch);

            // CodebaseHq format, see http://support.codebasehq.com/kb/howtos/repository-push-commit-notifications
            var repository = payload.Value<JObject>("repository");
            var urls = repository.Value<JObject>("clone_urls");
            info.IsPrivate = repository.Value<bool>("private");

            if (info.IsPrivate)
            {
                info.RepositoryUrl = urls.Value<string>("ssh");
            }
            else
            {
                // use http clone url if it's a public repo
                info.RepositoryUrl = urls.Value<string>("http");                
            }

            return info;
        }

        protected override string GetDeployer(HttpRequestBase request)
        {
            return "CodebaseHQ";
        }
    }
}