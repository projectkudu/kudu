using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Hooks;

namespace Kudu.Client.Diagnostics
{
    public class RemoteWebHooksManager : KuduRemoteClientBase
    {
        public RemoteWebHooksManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public Task<IEnumerable<WebHook>> GetWebHooksAsync()
        {
            return Client.GetJsonAsync<IEnumerable<WebHook>>(String.Empty);
        }

        public async Task<WebHook> SubscribeAsync(WebHook webHook)
        {
            return await Client.PostJsonAsync<WebHook, WebHook>(String.Empty, webHook);
        }

        public async Task UnsubscribeAsync(string hookId)
        {
            HttpResponseMessage response = await Client.DeleteAsync(hookId);
            response.EnsureSuccessful();
        }
    }
}