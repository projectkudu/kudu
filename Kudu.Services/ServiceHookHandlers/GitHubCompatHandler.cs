using System;
using System.IO;
using System.Linq;
using System.Web;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Default Servicehook Handler, uses github format.
    /// </summary>
    public class GitHubCompatHandler : ServiceHookHandlerBase
    {
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            GitDeploymentInfo gitDeploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
            if (gitDeploymentInfo != null && gitDeploymentInfo.IsValid())
            {
                deploymentInfo = gitDeploymentInfo;
                return IsDeleteCommit(gitDeploymentInfo.NewRef) ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }
            deploymentInfo = null;
            return DeployAction.UnknownPayload;
        }

        protected virtual GitDeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            JObject repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return null;
            }

            // The format of ref is refs/something/something else
            // e.g. refs/head/master or refs/head/foo/bar
            string branch = payload.Value<string>("ref");

            if (String.IsNullOrEmpty(branch) || !branch.StartsWith("refs/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            else
            {
                // Extract the name from refs/head/master notation.
                int secondSlashIndex = branch.IndexOf('/', 5);
                branch = branch.Substring(secondSlashIndex + 1);
                if (!branch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            var info = new GitDeploymentInfo { RepositoryType = RepositoryType.Git };
            // github format
            // { repository: { url: "https//...", private: False }, ref: "", before: "", after: "" } 
            info.RepositoryUrl = repository.Value<string>("url");
            info.Deployer = GetDeployer(request);
            info.NewRef = payload.Value<string>("after");
            var commits = payload.Value<JArray>("commits");

            info.TargetChangeset = ParseChangeSet(info.NewRef, commits);

            // private repo, use SSH
            bool isPrivate = repository.Value<bool>("private");
            if (isPrivate)
            {
                Uri uri = new Uri(info.RepositoryUrl);
                if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var host = "git@" + uri.Host;
                    info.RepositoryUrl = host + ":" + uri.AbsolutePath.TrimStart('/');
                }
            }

            return info;            
        }

        protected static ChangeSet ParseChangeSet(string id, JArray commits)
        {
            if (commits == null || !commits.HasValues)
            {
                return new ChangeSet(id, authorName: null, authorEmail: null, message: null, timestamp: DateTimeOffset.UtcNow);
            }
            JToken latestCommit = commits.Last;
            JObject commitAuthor = latestCommit.Value<JObject>("author");
            return new ChangeSet(
                    id,
                    commitAuthor.Value<string>("name"),
                    commitAuthor.Value<string>("email"),
                    latestCommit.Value<string>("message"),
                    latestCommit.Value<DateTime>("timestamp")
            );
        }

        protected virtual string GetDeployer(HttpRequestBase request)
        {
            return "External Provider";
        }

        protected static bool IsDeleteCommit(string newRef)
        {
            return newRef.All(c => c == '0');
        }
    }
}
