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

        public async Task<string> GetPublicKey(string key)
        {
            var param = new KeyValuePair<string, string>("key", key);
            string publicKey = await Client.PostJsonAsync<KeyValuePair<string, string>, string>(String.Empty, param);

            return publicKey;
        }
    }
}
