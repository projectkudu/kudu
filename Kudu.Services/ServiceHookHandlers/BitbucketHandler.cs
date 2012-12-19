using System;
using System.Linq;
using System.Web;
using Kudu.Contracts.SourceControl;
using Kudu.Core;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class BitbucketHandler : ServiceHookHandlerBase
    {
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith("Bitbucket", StringComparison.OrdinalIgnoreCase))
            {
                deploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                return deploymentInfo == null ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        protected DeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            // bitbucket format
            // { repository: { absolute_url: "/a/b", is_private: true }, canon_url: "https//..." } 
            var repository = payload.Value<JObject>("repository");
            var commits = payload.Value<JArray>("commits");
            

            // Identify the last commit for the target branch. 
            JObject targetCommit = (from commit in commits
                                    where targetBranch.Equals(commit.Value<string>("branch"), StringComparison.OrdinalIgnoreCase)
                                    orderby DateTimeOffset.Parse(commit.Value<string>("utctimestamp")) descending
                                    select (JObject)commit).FirstOrDefault();

            if (targetCommit == null)
            {
                return null;
            }

            var info = new DeploymentInfo();
            string server = payload.Value<string>("canon_url");     // e.g. https://bitbucket.org
            string path = repository.Value<string>("absolute_url"); // e.g. /davidebbo/testrepo/
            string scm = repository.Value<string>("scm"); // e.g. hg

            // Combine them to get the full URL
            info.RepositoryUrl = server + path;
            info.IsPrivate = repository.Value<bool>("is_private");
            info.RepositoryType = scm.Equals("hg", StringComparison.OrdinalIgnoreCase) ? RepositoryType.Mercurial : RepositoryType.Git;

            info.TargetChangeset = new ChangeSet(
                id: targetCommit.Value<string>("raw_node"),
                authorName: targetCommit.Value<string>("author"),  // The Bitbucket id for the user.
                authorEmail: null,                                 // TODO: Bitbucket gives us the raw_author field which is the user field set in the repository, maybe we should parse it.
                message: (targetCommit.Value<string>("message") ?? String.Empty).TrimEnd(),
                timestamp: DateTimeOffset.Parse(targetCommit.Value<string>("utctimestamp"))
            );

            info.Deployer = request.UserAgent;

            // private repo, use SSH
            if (info.IsPrivate)
            {
                Uri uri = new Uri(info.RepositoryUrl);
                if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    info.Host = "git@" + uri.Host;
                    info.RepositoryUrl = info.Host + ":" + uri.AbsolutePath.TrimStart('/');
                    info.UseSSH = true;
                }
            }

            // Identity the latest commit 

            return info;
        }
    }
}