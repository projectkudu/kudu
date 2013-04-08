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
            : base(serviceUrl, credentials)
        {
        }

        public Task SetPrivateKey(string key)
        {
            KeyValuePair<string, string>[] param = new KeyValuePair<string, string>[] 
            { 
                new KeyValuePair<string, string>("key", key)
            };

            return Client.PutAsync(String.Empty, param);
        }

        public Task<string> GetPublicKey(bool ensurePublicKey = false)
        {
            return Client.GetJsonAsync<string>(ensurePublicKey ?  "?ensurePublicKey=1" : String.Empty);
        }
    }
}
