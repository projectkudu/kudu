using System;
using System.Linq;
using System.Net;
using System.Web;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;
using Kudu.Core.Deployment;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Handler for Bitbucket Webhook, where we only consume "PUSH" payload
    /// </summary>
    public class BitbucketHandlerV2 : ServiceHookHandlerBase
    {
        public BitbucketHandlerV2(IRepositoryFactory repositoryFactory)
            : base(repositoryFactory)
        {
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase deploymentInfo)
        {
            deploymentInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith("Bitbucket-Webhooks/2.0", StringComparison.OrdinalIgnoreCase))
            {
                deploymentInfo = GetDeploymentInfo(payload, targetBranch);
                return deploymentInfo == null ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        protected DeploymentInfoBase GetDeploymentInfo(JObject payload, string targetBranch)
        {
            var info = new DeploymentInfo(RepositoryFactory)
            {
                Deployer = "Bitbucket",
                IsContinuous = true
            };

            // get changes
            // do not handle NullRef, let if fail if payload is not in the right format
            //{
            //    "push": {
            //        "changes": [
            //            {
            //                "old": { ... },
            //                "new": { ... }
            //            }
            //        ]
            //    }
            //}
            var changes = payload.Value<JObject>("push").Value<JArray>("changes");
            if (changes != null && changes.Count > 0)
            {
                JObject latestCommit = (from change in changes
                                        where change.Value<JObject>("new") != null && string.Equals(targetBranch, change.Value<JObject>("new").Value<string>("name") ?? targetBranch, StringComparison.OrdinalIgnoreCase)
                                        orderby BitbucketHandler.TryParseCommitStamp(change.Value<JObject>("new").Value<JObject>("target").Value<string>("date")) descending
                                        select change.Value<JObject>("new")).FirstOrDefault();

                if (latestCommit == null)
                {
                    return null;
                }

                string authorRaw = WebUtility.HtmlDecode(latestCommit.Value<JObject>("target").Value<JObject>("author").Value<string>("raw"));
                string[] nameAndEmail = authorRaw.Split(new char[] { '<', '>' }, StringSplitOptions.RemoveEmptyEntries);
                info.TargetChangeset = new ChangeSet(
                    id: latestCommit.Value<JObject>("target").Value<string>("hash"),
                    authorName: nameAndEmail.Length == 2 ? nameAndEmail[0].Trim() : authorRaw,
                    authorEmail: nameAndEmail.Length == 2 ? nameAndEmail[1].Trim() : null,
                    message: latestCommit.Value<JObject>("target").Value<string>("message"),
                    timestamp: BitbucketHandler.TryParseCommitStamp(latestCommit.Value<JObject>("target").Value<string>("date")));
            }
            else
            {
                info.TargetChangeset = new ChangeSet(id: String.Empty, authorName: null,
                                        authorEmail: null, message: null, timestamp: DateTime.UtcNow);
            }

            info.RepositoryUrl = payload.Value<JObject>("repository").Value<JObject>("links").Value<JObject>("html").Value<string>("href");
            bool isGitRepo = string.Equals("git", payload.Value<JObject>("repository").Value<string>("scm"), StringComparison.OrdinalIgnoreCase);
            info.RepositoryType = isGitRepo ? RepositoryType.Git : RepositoryType.Mercurial;

            bool isPrivate = bool.Parse(payload.Value<JObject>("repository").Value<string>("is_private"));
            // private repo, use SSH
            if (isPrivate)
            {
                info.RepositoryUrl = BitbucketHandler.GetPrivateRepoUrl(info.RepositoryUrl, info.RepositoryType);
            }

            return info;
        }
    }
}
