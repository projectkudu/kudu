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
            deploymentInfo = null;
            string deployer = payload.Value<string>("deployer");
            if (!"CodePlex".Equals(deployer, StringComparison.OrdinalIgnoreCase))
            {
                return DeployAction.UnknownPayload;
            }

            string newRef = payload.Value<string>("newRef");
            string scm = payload.Value<string>("scmType");
            string branch = payload.Value<string>("branch");

            deploymentInfo = new DeploymentInfo
            {
                RepositoryUrl = payload.Value<string>("url"),
                Deployer = "CodePlex",
                TargetChangeset = new ChangeSet(newRef, authorName: null, authorEmail: null, message: null, timestamp: DateTimeOffset.Now)
            };

            if ("Mercurial".Equals(scm, StringComparison.OrdinalIgnoreCase))
            {
                // { url: "", branch: "", deployer: "", scmType: "Mercurial" }
                deploymentInfo.RepositoryType = RepositoryType.Mercurial;
                // TODO: Figure out what happens when you delete the branch and handle that.
                return targetBranch.Equals(branch, StringComparison.OrdinalIgnoreCase) ? DeployAction.ProcessDeployment : DeployAction.NoOp;
            }
            else
            {
                // Look for the generic format
                // { url: "", branch: "", deployer: "", oldRef: "", newRef: "", scmType: "Git" } 
                
                
                deploymentInfo.RepositoryType = RepositoryType.Git;
                
                // Ignore the deployment request (a) if the target branch does not match the deployed branch or (b) if the newRef is all zero (deleted changesets)
                return (deploymentInfo.IsValid() && (!targetBranch.Equals(branch, StringComparison.OrdinalIgnoreCase) || newRef.All(c => c == '0'))) ? 
                        DeployAction.NoOp : DeployAction.ProcessDeployment;
            }
        }
    }
}
