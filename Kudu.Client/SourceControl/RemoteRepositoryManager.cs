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

        public Task<RepositoryInfo> GetRepositoryInfo()
        {
            return _client.GetJsonAsync<RepositoryInfo>("info");
        }
    }
}
