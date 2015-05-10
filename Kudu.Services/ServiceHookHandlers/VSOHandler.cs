using System;
using System.Linq;
using System.Web;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Default Servicehook Handler, uses VSO format.
    /// </summary>
    public class VSOHandler : ServiceHookHandlerBase
    {
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;

            var publisherId = payload.Value<string>("publisherId");
            if (String.Equals(publisherId, "tfs", StringComparison.OrdinalIgnoreCase))
            {
                GitDeploymentInfo gitDeploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                deploymentInfo = gitDeploymentInfo;
                return deploymentInfo == null || IsDeleteCommit(gitDeploymentInfo.NewRef) ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        protected virtual GitDeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            // the rest must exist or NullRef.
            // this allows us to identify the culprit handler
            var sessionToken = payload.Value<JObject>("sessionToken");
            var resource = payload.Value<JObject>("resource");
            var refUpdates = resource.Value<JArray>("refUpdates");
            var repository = resource.Value<JObject>("repository");

            // The format of ref is refs/something/something else
            // e.g. refs/head/master or refs/head/foo/bar
            string branch = refUpdates.Last.Value<string>("name");
            // Extract the name from refs/head/master notation.
            int secondSlashIndex = branch.IndexOf('/', 5);
            branch = branch.Substring(secondSlashIndex + 1);
            if (!branch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var info = new GitDeploymentInfo { RepositoryType = RepositoryType.Git };
            info.Deployer = GetDeployer(request);
            info.NewRef = refUpdates.Last.Value<string>("newObjectId");

            var builder = new UriBuilder(repository.Value<string>("remoteUrl"));
            builder.UserName = sessionToken.Value<string>("token");
            builder.Password = String.Empty;
            info.RepositoryUrl = builder.Uri.AbsoluteUri;

            var commits = resource.Value<JArray>("commits");
            info.CommitId = refUpdates.Last.Value<string>("newObjectId");
            info.TargetChangeset = ParseChangeSet(info.CommitId, commits);
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
                    latestCommit.Value<string>("comment"),
                    commitAuthor.Value<DateTime>("date")
            );
        }

        protected virtual string GetDeployer(HttpRequestBase request)
        {
            return "VSO";
        }

        protected static bool IsDeleteCommit(string newRef)
        {
            return newRef.All(c => c == '0');
        }
    }
}
