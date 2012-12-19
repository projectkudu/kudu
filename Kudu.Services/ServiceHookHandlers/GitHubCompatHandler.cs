using System;
using System.IO;
using System.Linq;
using System.Web;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Default Servicehook Handler, uses github format.
    /// </summary>
    public class GitHubCompatHandler : ServiceHookHandlerBase
    {
        protected readonly IGitServer _gitServer;

        public GitHubCompatHandler(IGitServer gitServer)
        {
            _gitServer = gitServer;
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
            if (deploymentInfo != null && deploymentInfo.IsValid())
            {
                return DeployAction.ProcessDeployment;
            }
            return DeployAction.UnknownPayload;
        }

        protected virtual DeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            JObject repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return null;
            }

            // The format of ref is refs/something/something else
            // For master it's normally refs/head/master
            string branch = payload.Value<string>("ref");

            if (String.IsNullOrEmpty(branch))
            {
                return null;
            }
            else
            {
                // Extract the name from /refs/head/master notation.
                branch = Path.GetFileName(branch);
                if (!branch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            var latestCommit = repository.Value<JArray>("commits").LastOrDefault();
            if (latestCommit == null)
            {
                // If the commits list is empty, it is likely that the push was a branch delete.
                return null;
            }
            
            var info = new DeploymentInfo();

            // github format
            // { repository: { url: "https//...", private: False }, ref: "", before: "", after: "" } 
            info.RepositoryUrl = repository.Value<string>("url");
            info.IsPrivate = repository.Value<bool>("private");
            info.Deployer = GetDeployer(request);

            var commitAuthor = latestCommit.Value<JObject>("committer");
            info.TargetChangeset = new ChangeSet(
                    latestCommit.Value<string>("id"), 
                    commitAuthor.Value<string>("name"), 
                    commitAuthor.Value<string>("email"),
                    latestCommit.Value<string>("message"),
                    latestCommit.Value<DateTimeOffset>("timestamp"));

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

            return info;            
        }

        protected virtual string GetDeployer(HttpRequestBase request)
        {
            return "External Provider";
        }
    }
}