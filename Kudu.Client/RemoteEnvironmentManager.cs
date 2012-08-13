using System;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;

namespace Kudu.Client
{
    public class RemoteEnvironmentManager : KuduRemoteClientBase
    {
        public RemoteEnvironmentManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public RemoteEnvironmentManager(string serviceUrl, HttpMessageHandler handler)
            : base(serviceUrl, handler)
        {
        }
    }
}
