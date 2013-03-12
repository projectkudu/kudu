using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Core.SourceControl;

namespace Kudu.Client.Deployment
{
    public class RemoteFetchManager : KuduRemoteClientBase
    {
        public RemoteFetchManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public Task TriggerFetch(string repositoryUrl, RepositoryType repositoryType)
        {
            var parameters = new[]
            {
                new KeyValuePair<string, string>("format", "basic"),
                new KeyValuePair<string, string>("url", repositoryUrl),
                new KeyValuePair<string, string>("scm", repositoryType == RepositoryType.Mercurial ? "hg" : null)
            };
            return Client.PostAsync("", parameters);
        }
    }
}
