using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.SSHKey
{
    public class RemoteSSHKeyManager : KuduRemoteClientBase
    {
        public RemoteSSHKeyManager(string serviceUrl, ICredentials credentials = null)
            : base(UrlUtility.EnsureTrailingSlash(serviceUrl), credentials)
        {
        }

        public Task SetPrivateKey(string key)
        {
            KeyValuePair<string, string>[] param = new KeyValuePair<string, string>[] 
            { 
                new KeyValuePair<string, string>("key", key)
            };

            return Client.PutAsync(String.Empty, param).Then(response =>
            {
                response.EnsureSuccessful();
                return;
            });
        }
    }
}
