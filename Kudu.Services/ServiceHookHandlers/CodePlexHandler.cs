using System;
using System.Linq;
using System.Web;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class CodePlexHandler : ServiceHookHandlerBase
    {
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            // Look for the generic format
            // { url: "", branch: "", deployer: "", oldRef: "", newRef: "" } 
            deploymentInfo = new DeploymentInfo
            {
                RepositoryUrl = payload.Value<string>("url"),
                Deployer = payload.Value<string>("deployer"),
                TargetChangeset = new ChangeSet(payload.Value<string>("newRef"), authorName: String.Empty, authorEmail: String.Empty, message: String.Empty, timestamp: DateTimeOffset.Now)
            };

            if (!deploymentInfo.IsValid() || !"CodePlex".Equals(deploymentInfo.Deployer, StringComparison.OrdinalIgnoreCase))
            {
                return DeployAction.UnknownPayload;
            }

            string branch = payload.Value<string>("branch");
            string newRef = payload.Value<string>("newRef");

            // Ignore the deployment request (a) if the target branch does not match the deployed branch or (b) if the newRef is all zero (deleted changesets)
            return (!targetBranch.Equals(branch, StringComparison.OrdinalIgnoreCase) || newRef.All(c => c == '0')) ?  DeployAction.NoOp :  DeployAction.ProcessDeployment;
        }


    }
}