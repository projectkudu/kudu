using System;
using System.Web;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Default Servicehook Handler, uses github format.
    /// </summary>
    public class GitHubCompatHandler : IServiceHookHandler
    {
        protected readonly IGitServer _gitServer;

        public GitHubCompatHandler(IGitServer gitServer)
        {
            _gitServer = gitServer;
        }

        public virtual DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
            if (deploymentInfo != null && deploymentInfo.IsValid())
            {
                return DeployAction.ProcessDeployment;
            }
            return DeployAction.UnknownPayload;
        }

        public virtual void Fetch(DeploymentInfo repositoryInfo, string targetBranch)
        {
            // Fetch from url
            _gitServer.FetchWithoutConflict(repositoryInfo.RepositoryUrl, "external", targetBranch);
        }

        protected virtual DeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            JObject repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return null;
            }

            var info = new DeploymentInfo();

            // github format
            // { repository: { url: "https//...", private: False }, ref: "", before: "", after: "" } 
            info.RepositoryUrl = repository.Value<string>("url");
            info.IsPrivate = repository.Value<bool>("private");

            // The format of ref is refs/something/something else
            // For master it's normally refs/head/master
            string @ref = payload.Value<string>("ref");

            if (String.IsNullOrEmpty(@ref))
            {
                return null;
            }

            info.Deployer = GetDeployer(request);

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