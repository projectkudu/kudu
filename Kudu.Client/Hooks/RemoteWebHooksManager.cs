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

        public async Task SubscribeAsync(WebHook webHook)
        {
            HttpResponseMessage response = await Client.PostAsJsonAsync("subscribe", webHook);
            response.EnsureSuccessful();
        }

        public async Task UnsubscribeAsync(string hookAddress)
        {
            HttpResponseMessage response = await Client.DeleteAsync("unsubscribe?hookAddress=" + Uri.EscapeUriString(hookAddress));
            response.EnsureSuccessful();
        }
    }
}