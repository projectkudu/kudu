using System.Web;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public interface IServiceHookHandler
    {
        bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo);
    }
}