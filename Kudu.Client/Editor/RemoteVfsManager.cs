using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.Editor
{
    public class RemoteVfsManager : KuduRemoteClientBase
    {
        public RemoteVfsManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }
    }
}
