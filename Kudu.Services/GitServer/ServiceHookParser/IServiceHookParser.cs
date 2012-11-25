using System.Web;

namespace Kudu.Services.GitServer.ServiceHookParser
{
    public interface IServiceHookParser
    {
        bool TryGetRepositoryInfo(HttpRequest request, string body, out RepositoryInfo repositoryInfo);
    }
}