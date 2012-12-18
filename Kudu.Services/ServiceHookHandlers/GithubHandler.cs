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
                deploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                return deploymentInfo == null ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }
            return DeployAction.UnknownPayload;
        }

        protected override string GetDeployer(HttpRequestBase request)
        {
            return "GitHub";
        }
    }
}