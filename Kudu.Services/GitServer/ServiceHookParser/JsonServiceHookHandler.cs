using System;
using System.IO;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookParser
{
    /// <summary>
    /// Default Servicehook Handler, uses github format.
    /// </summary>
    public class JsonServiceHookHandler : IServiceHookHandler
    {
        public virtual bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo)
        {
            string json = String.Empty;
            if (request.Form.Count > 0)
            {
                json = request.Form["payload"];
                if (String.IsNullOrEmpty(json))
                {
                    json = request.Form[0];
                }
            }
            else
            {
                // assume raw json
                request.InputStream.Seek(0, SeekOrigin.Begin);
                json = new StreamReader(request.InputStream).ReadToEnd();
            }

            JObject payload = JObject.Parse(json);

            repositoryInfo = GetRepositoryInfo(request, payload);
            return repositoryInfo != null;
        }

        protected virtual RepositoryInfo GetRepositoryInfo(HttpRequest request, JObject payload)
        {
            JObject repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return null;
            }

            var info = new RepositoryInfo();

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

            return info;            
        }

        protected virtual string GetDeployer(HttpRequest request)
        {
            // looks like github, 
            return "GitHub compatible";
        }
    }
}