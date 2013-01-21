using System;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class CodebaseHqHandler : GitHubCompatHandler
    {
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith("Codebasehq", StringComparison.OrdinalIgnoreCase))
            {
                GitDeploymentInfo gitDeploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                deploymentInfo = gitDeploymentInfo;
                return deploymentInfo == null || IsDeleteCommit(gitDeploymentInfo.NewRef) ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        protected override GitDeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            var info = base.GetDeploymentInfo(request, payload, targetBranch);

            if (info == null)
            {
                return null;
            }

            // CodebaseHq format, see http://support.codebasehq.com/kb/howtos/repository-push-commit-notifications
            var repository = payload.Value<JObject>("repository");
            var urls = repository.Value<JObject>("clone_urls");
            var isPrivate = repository.Value<bool>("private");
            info.NewRef = payload.Value<string>("after");

            if (isPrivate)
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