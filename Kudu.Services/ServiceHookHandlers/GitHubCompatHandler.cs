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
            // check if this parser can be used to interprete request
            if (ParserMatches(request, payload, targetBranch))
            { 
                // recognize request, decide whether it want to process it
                if (IsNoop(request, payload, targetBranch))
                {
                    // Noop, deployment info is never used
                    deploymentInfo = null;
                    return DeployAction.NoOp;
                }

                // fill deploymentinfo body
                deploymentInfo = new DeploymentInfo { RepositoryType = RepositoryType.Git, IsContinuous = true };
                var commits = payload.Value<JArray>("commits");
                string newRef = payload.Value<string>("after");
                deploymentInfo.TargetChangeset = ParseChangeSet(newRef, commits);
                deploymentInfo.RepositoryUrl = DetermineSecurityProtocol(payload);
                deploymentInfo.Deployer = GetDeployer();

                return DeployAction.ProcessDeployment;
            }

            deploymentInfo = null;
            return DeployAction.UnknownPayload;
        } 

        protected virtual bool IsNoop(HttpRequestBase request, JObject payload, string targetBranch)
        { 
            string newRef = payload.Value<string>("after");
            return IsDeleteCommit(newRef);
        }

        protected virtual bool ParserMatches(HttpRequestBase request, JObject payload, string targetBranch)
        {
            JObject repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return false;
            }

            // The format of ref is refs/something/something else
            // e.g. refs/head/master or refs/head/foo/bar
            string branch = payload.Value<string>("ref");

            if (String.IsNullOrEmpty(branch) || !branch.StartsWith("refs/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else
            {
                // Extract the name from refs/head/master notation.
                int secondSlashIndex = branch.IndexOf('/', 5);
                branch = branch.Substring(secondSlashIndex + 1);
                if (!branch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        protected virtual string DetermineSecurityProtocol(JObject payload)
        {
            // keep the old code
            JObject repository = payload.Value<JObject>("repository");
            string repositoryUrl = repository.Value<string>("url");
            // private repo, use SSH
            bool isPrivate = repository.Value<bool>("private");
            if (isPrivate)
            {
                Uri uri = new Uri(repositoryUrl);
                if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var host = "git@" + uri.Host;
                    repositoryUrl = host + ":" + uri.AbsolutePath.TrimStart('/');
                }
            }
            return repositoryUrl;
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

        protected virtual string GetDeployer()
        {
            return "External Provider";
        }

        protected static bool IsDeleteCommit(string newRef)
        {
            return newRef.All(c => c == '0');
        }
    }
}
