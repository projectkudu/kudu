using System;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookParser
{
    public class Github : IServiceHookParser
    {
        public bool TryGetRepositoryInfo(HttpRequest request, Lazy<string> bodyDontUse, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (request.Headers["X-Github-Event"] == null)
            {
                return false;
            }

            string json = request.Form["payload"];
            JObject payload = JObject.Parse(json);

            var repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return false;
            }

            var info = new RepositoryInfo();

            // github format
            // { repository: { url: "https//...", private: False }, ref: "", before: "", after: "" } 
            info.RepositoryUrl = repository.Value<string>("url");

            if (string.IsNullOrEmpty(info.RepositoryUrl))
            {
                return false;
            }

            info.IsPrivate = repository.Value<bool>("private");

            // The format of ref is refs/something/something else
            // For master it's normally refs/head/master
            string @ref = payload.Value<string>("ref");

            if (String.IsNullOrEmpty(@ref))
            {
                return false;
            }

            info.Deployer = GetDeployer(request);
            info.OldRef = payload.Value<string>("before");
            info.NewRef = payload.Value<string>("after");

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

            repositoryInfo = info;
            return true;
        }

        private string GetDeployer(HttpRequest request)
        {
            if (request.Headers["X-Github-Event"] != null)
            {
                return "GitHub";
            }

            // looks like github, 
            return "GitHub compatible";
        }
    }
}