using System.Net;
using Kudu.Client.Infrastructure;

namespace Kudu.Client
{
    public class RemoteEnvironmentManager : KuduRemoteClientBase
    {
        public RemoteEnvironmentManager(string serviceUrl, ICredentials credentials = null)
            : base(UrlUtility.EnsureTrailingSlash(serviceUrl), credentials)
        {
        }
    }
}
