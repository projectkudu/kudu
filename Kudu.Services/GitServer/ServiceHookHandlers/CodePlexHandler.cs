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

        public bool TryGetRepositoryInfo(HttpRequest request, JObject payload, out RepositoryInfo repositoryInfo)
        {
            // Look for the generic format
            // { url: "", branch: "", deployer: "", oldRef: "", newRef: "" } 
            repositoryInfo = new RepositoryInfo
            {
                RepositoryUrl = payload.Value<string>("url"),
                Deployer = payload.Value<string>("deployer"),
                OldRef = payload.Value<string>("oldRef"),
                NewRef = payload.Value<string>("newRef")
            };

            return repositoryInfo.IsValid();
        }

        public virtual void Fetch(RepositoryInfo repositoryInfo, string targetBranch)
        {
            // Fetch from url
            _gitServer.FetchWithoutConflict(repositoryInfo.RepositoryUrl, "external", targetBranch);
        }
    }
}