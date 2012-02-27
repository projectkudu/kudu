using System;
using System.Collections.Generic;
using System.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Client.Infrastructure
{
    internal static class HttpClientHelper
    {
        public static HttpClient Create(string url)
        {
            // The URL needs to end with a slash for HttpClient to do the right thing with relative paths
            url = UrlUtility.EnsureTrailingSlash(url);

            var handler = new TrailingSlashHandler();

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(url),
                MaxResponseContentBufferSize = 30 * 1024 * 1024
            };

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static HttpContent CreateJsonContent(params KeyValuePair<string, string>[] items)
        {
            var jsonObject = new JsonObject();
            foreach (KeyValuePair<string, string> kv in items)
            {
                jsonObject.Add(kv.Key, kv.Value);
            }
            return new ObjectContent(typeof(JsonObject), jsonObject, "application/json");
        }

        private class TrailingSlashHandler : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Remove trailing slash before any request is made (since credentials are lost when a 307 is returned when auth is enabled)
                string url = request.RequestUri.OriginalString;
                if (url.EndsWith("/"))
                {
                    url = url.Substring(0, url.Length - 1);
                }

                request.RequestUri = new Uri(url);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
