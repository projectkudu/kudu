using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Editor;

namespace Kudu.Client.Editor
{
    public class RemoteVfsManager : KuduRemoteClientBase
    {
        public RemoteVfsManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public async Task<string> ReadAllTextAsync(string path)
        {
            using (HttpResponseMessage response = await Client.GetAsync(path))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                return await response
                                .EnsureSuccessful()
                                .Content
                                .ReadAsStringAsync();
            }
        }

        public Task<IEnumerable<VfsStatEntry>> ListAsync(string path)
        {
            return Client.GetJsonAsync<IEnumerable<VfsStatEntry>>(path);
        }

        public string ReadAllText(string path)
        {
            return ReadAllTextAsync(path).Result;
        }

        public bool Exists(string path)
        {
            HttpResponseMessage response = GetHeadResponse(path);
            return response.StatusCode == HttpStatusCode.OK;
        }

        public void WriteAllText(string path, string content)
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Put;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                request.Content = new StringContent(content);
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }

        public void WriteAllBytes(string path, byte[] buffer)
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Put;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                request.Content = new StreamContent(new MemoryStream(buffer));
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }

        public void Delete(string path, bool recursive = false)
        {
            using (var request = new HttpRequestMessage())
            {
                path += recursive ? "?recursive=true" : String.Empty;

                request.Method = HttpMethod.Delete;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);

                HttpResponseMessage response = Client.SendAsync(request).Result;

                // Don't throw if we got a 404, since we want to act as a 'delete if exists'
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessful();
                }
            }
        }

        private HttpResponseMessage GetHeadResponse(string path)
        {
            using (var headRequest = new HttpRequestMessage())
            {
                headRequest.Method = HttpMethod.Head;
                headRequest.RequestUri = new Uri(path, UriKind.Relative);
                return Client.SendAsync(headRequest).Result;
            }
        }
    }
}
