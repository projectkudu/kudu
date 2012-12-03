using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Client.SourceControl
{
    public class RemoteRepositoryManager : KuduRemoteClientBase
    {
        public RemoteRepositoryManager(string serviceUrl, ICredentials credentials = null)
            : base(UrlUtility.EnsureTrailingSlash(serviceUrl), credentials)
        {
        }

        public Task<RepositoryInfo> GetRepositoryInfo()
        {
            return Client.GetJsonAsync<RepositoryInfo>("info");
        }

        public Task Delete(bool deleteWebRoot = false)
        {
            return Client.DeleteSafeAsync(deleteWebRoot ? "?deleteWebRoot=1" : String.Empty);
        }
    }
}
