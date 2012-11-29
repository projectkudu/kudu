using System;
using System.Web;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class BitbucketHandler : IServiceHookHandler
    {
        private readonly IGitServer _gitServer;

        public BitbucketHandler(IGitServer gitServer)
        {
            _gitServer = gitServer;
        }

        public bool TryGetRepositoryInfo(HttpRequest request, JObject payload, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith("Bitbucket", StringComparison.OrdinalIgnoreCase))
            {
                repositoryInfo = GetRepositoryInfo(request, payload);
            }

            return repositoryInfo != null;
        }

        public void Fetch(RepositoryInfo repositoryInfo, string targetBranch)
        {
            // Fetch from url
            _gitServer.FetchWithoutConflict(repositoryInfo.RepositoryUrl, "external", targetBranch);
        }

        private RepositoryInfo GetRepositoryInfo(HttpRequest request, JObject payload)
        {
            // bitbucket format
            // { repository: { absolute_url: "/a/b", is_private: true }, canon_url: "https//..." } 
            var repository = payload.Value<JObject>("repository");

            var info = new RepositoryInfo();
            string server = payload.Value<string>("canon_url");     // e.g. https://bitbucket.org
            string path = repository.Value<string>("absolute_url"); // e.g. /davidebbo/testrepo/

            // Combine them to get the full URL
            info.RepositoryUrl = server + path;
            info.IsPrivate = repository.Value<bool>("is_private");

            // We don't get any refs from bitbucket, so write dummy string (we ignore it later anyway)
            info.OldRef = "dummy";

            // When there are no commits, set the new ref to an all-zero string to cause the logic in
            // GitDeploymentRepository.GetReceiveInfo ignore the push
            var commits = payload.Value<JArray>("commits");
            info.NewRef = commits.Count == 0 ? "000" : "dummy";

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

            return info;
        }
    }
}