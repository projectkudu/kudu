using System;
using System.Web;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class CodePlexHandler : IServiceHookHandler
    {
        protected readonly IGitServer _gitServer;

        public CodePlexHandler(IGitServer gitServer)
        {
            _gitServer = gitServer;
        }

        public bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;

            string json = request.Form["payload"];
            if (String.IsNullOrEmpty(json))
            {
                return false;
            }

            JObject payload = JObject.Parse(json);

            // Look for the generic format
            // { url: "", branch: "", deployer: "", oldRef: "", newRef: "" } 
            repositoryInfo = new RepositoryInfo
            {
                RepositoryUrl = payload.Value<string>("url"),
                Deployer = payload.Value<string>("deployer"),
                OldRef = payload.Value<string>("oldRef"),
                NewRef = payload.Value<string>("newRef")
            };

            return !String.IsNullOrEmpty(repositoryInfo.RepositoryUrl) &&
                !String.IsNullOrEmpty(repositoryInfo.Deployer) &&
                !String.IsNullOrEmpty(repositoryInfo.OldRef) &&
                !String.IsNullOrEmpty(repositoryInfo.NewRef);
        }

        public virtual void Fetch(RepositoryInfo repositoryInfo, string targetBranch)
        {
            // Fetch from url
            _gitServer.FetchWithoutConflict(repositoryInfo.RepositoryUrl, "external", targetBranch);
        }
    }
}