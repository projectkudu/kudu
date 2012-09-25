using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Client.SSHKey
{
    public class RemoteSSHKeyManager : KuduRemoteClientBase
    {
        public RemoteSSHKeyManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public RemoteSSHKeyManager(string serviceUrl, HttpMessageHandler handler)
            : base(serviceUrl, handler)
        {
        }

        public Task SetPrivateKey(string key)
        {
            KeyValuePair<string, string>[] param = new KeyValuePair<string, string>[] 
            { 
                new KeyValuePair<string, string>("key", key)
            };

            return _client.PutAsync(string.Empty, param).Then(response =>
            {
                response.EnsureSuccessStatusCode();
                return;
            });
        }
    }
}
