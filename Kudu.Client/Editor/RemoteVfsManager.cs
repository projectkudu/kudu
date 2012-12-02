using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.Editor
{
    public class RemoteVfsManager : KuduRemoteClientBase
    {
        public RemoteVfsManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public string ReadAllText(string path)
        {
            HttpResponseMessage response = Client.GetAsync(path).Result;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return response
                .EnsureSuccessful()
                .Content
                .ReadAsStringAsync()
                .Result;
        }

        public void WriteAllText(string path, string content)
        {
            // First make a HEAD response to get the ETag (if the file exists)
            HttpResponseMessage response = GetHeadResponse(path);

            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Put;
                request.RequestUri = new Uri(path, UriKind.Relative);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    request.Headers.IfMatch.Add(response.Headers.ETag);
                }
                request.Content = new StringContent(content);
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }

        public void Delete(string path)
        {
            // First make a HEAD response to get the ETag (if the file exists)
            HttpResponseMessage response = GetHeadResponse(path);

            // Nothing to delete if it doesn't exist
            if (response.StatusCode == HttpStatusCode.NotFound) return;

            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Delete;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(response.Headers.ETag);
                Client.SendAsync(request).Result.EnsureSuccessful();
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
