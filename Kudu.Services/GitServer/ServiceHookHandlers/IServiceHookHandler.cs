using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public interface IServiceHookHandler
    {
        bool TryGetRepositoryInfo(HttpRequest request, JObject payload, out RepositoryInfo repositoryInfo);

        void Fetch(RepositoryInfo repositoryInfo, string targetBranch);
    }
}