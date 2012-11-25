using System;
using System.Web;

namespace Kudu.Services.GitServer.ServiceHookParser
{
    public interface IServiceHookParser
    {
        bool TryGetRepositoryInfo(HttpRequest request, Lazy<string> body, out RepositoryInfo repositoryInfo);
    }
}