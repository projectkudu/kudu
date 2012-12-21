using System;
using System.Web;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class GitHubHandler : GitHubCompatHandler
    {
        public GitHubHandler(IGitServer gitServer)
            : base(gitServer)
        {
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (request.Headers["X-Github-Event"] != null)
            {
                GitDeploymentInfo gitDeploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                deploymentInfo = gitDeploymentInfo;
                return deploymentInfo == null || IsDeleteCommit(gitDeploymentInfo.NewRef) ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }
            return DeployAction.UnknownPayload;
        }

        protected override string GetDeployer(HttpRequestBase request)
        {
            return "GitHub";
        }
    }
}