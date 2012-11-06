using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;

#if _STREAM_CLIENT_SUPPORT
namespace Kudu.Client.Diagnostics
{
    public class RemoteLogStreamManager : KuduRemoteClientBase
    {
        public RemoteLogStreamManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public RemoteLogStreamManager(string serviceUrl, HttpMessageHandler handler)
            : base(serviceUrl, handler)
        {
        }

        public Task<Stream> GetStream()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ServiceUrl);

            // ResponseHeadersRead option is to return once the header was read.
            return _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Then(response =>
            {
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStreamAsync();
            });
        }
    }
}
#endif
