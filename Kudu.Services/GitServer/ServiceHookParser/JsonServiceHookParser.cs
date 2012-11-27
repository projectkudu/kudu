using System;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookParser
{
    public abstract class JsonServiceHookParser : IServiceHookParser
    {
        public virtual bool TryGetRepositoryInfo(HttpRequest request, Lazy<string> body, out RepositoryInfo repositoryInfo)
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
                json = body.Value;
            }

            JObject payload = JObject.Parse(json);

            repositoryInfo = GetRepositoryInfo(request, payload);
            return repositoryInfo != null;
        }

        protected abstract RepositoryInfo GetRepositoryInfo(HttpRequest request, JObject payload);
    }
}