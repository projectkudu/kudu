using System;
using System.Web;

namespace Kudu.Services.GitServer.ServiceHookParser
{
    public interface IServiceHookHandler
    {
        bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo);
    }
}