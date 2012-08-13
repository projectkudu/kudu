using System;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Client.SourceControl
{
    public class RemoteRepositoryManager : KuduRemoteClientBase
    {
        public RemoteRepositoryManager(string serviceUrl)
            :base(serviceUrl)
        {

        }

        public RemoteRepositoryManager(string serviceUrl, HttpMessageHandler handler)
            : base(serviceUrl, handler)
        {

        }

        public Task<RepositoryInfo> GetRepositoryInfo()
        {
            return _client.GetJsonAsync<RepositoryInfo>("info");
        }

        public Task Delete()
        {
            return _client.DeleteSafeAsync(String.Empty);
        }
    }
}
