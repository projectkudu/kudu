using System;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class BitbucketHandler : JsonServiceHookHandler
    {
        public override bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith("Bitbucket", StringComparison.OrdinalIgnoreCase))
            {
                return base.TryGetRepositoryInfo(request, out repositoryInfo);
            }
            return false;
        }

        protected override RepositoryInfo GetRepositoryInfo(HttpRequest request, JObject payload)
        {
            // bitbucket format
            // { repository: { absolute_url: "/a/b", is_private: true }, canon_url: "https//..." } 
            var repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return null;
            }

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

            return info;
        }
    }
}