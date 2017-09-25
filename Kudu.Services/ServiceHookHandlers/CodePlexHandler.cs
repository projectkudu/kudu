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

            // { url: "", branch: "", deployer: "", oldRef: "", newRef: "", scmType: "Git" } 
            string newRef = payload.Value<string>("newRef");
            string scm = payload.Value<string>("scmType");
            string branch = payload.Value<string>("branch");

            deploymentInfo = new DeploymentInfo
            {
                RepositoryUrl = payload.Value<string>("url"),
                Deployer = "CodePlex",
                IsContinuous = true,
                TargetChangeset = new ChangeSet(newRef, authorName: null, authorEmail: null, message: null, timestamp: DateTimeOffset.Now)
            };

            deploymentInfo.RepositoryType = ("Mercurial".Equals(scm, StringComparison.OrdinalIgnoreCase)) ? RepositoryType.Mercurial : RepositoryType.Git;

            // Ignore the deployment request 
            // (a) if the target branch does not exist (null or empty string). This happens during test hook
            // (b) if the target branch does not have the same name as the deployed branch
            // (c) if the newRef is all zero (deleted changesets)
            return (String.IsNullOrEmpty(branch) || targetBranch.Equals(branch, StringComparison.OrdinalIgnoreCase)) && newRef.Any(c => c != '0') ?
                   DeployAction.ProcessDeployment : DeployAction.NoOp;
        }
    }
}
