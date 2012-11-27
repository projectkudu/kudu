using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class FallbackHandler : IServiceHookHandler
    {
        public bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo)
        {
            string json = request.Form["payload"];
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
            return true;
        }
    }
}