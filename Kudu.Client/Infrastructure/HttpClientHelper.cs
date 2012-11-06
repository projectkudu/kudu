using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kudu.Client.Infrastructure
{
    internal static class HttpClientHelper
    {
        public static HttpClient Create(string url, HttpMessageHandler handler)
        {
            // The URL needs to end with a slash for HttpClient to do the right thing with relative paths
            url = UrlUtility.EnsureTrailingSlash(url);

            var slashHandler = new TrailingSlashHandler()
            {
                InnerHandler = handler ?? new HttpClientHandler()
            };

            var client = new HttpClient(slashHandler)
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
            var jsonObject = new JObject();
            foreach (KeyValuePair<string, string> kv in items)
            {
                jsonObject.Add(kv.Key, kv.Value);
            }
            return new ObjectContent(typeof(JObject), jsonObject, new JsonMediaTypeFormatter());
        }

        private class TrailingSlashHandler : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Remove trailing slash before any request is made (since credentials are lost when a 307 is returned when auth is enabled)
                string url = request.RequestUri.OriginalString;
                if (url.EndsWith("/"))
                {
                    url = url.Substring(0, url.Length - 1);
                }

                // Handle query strings
                url = url.Replace("/?", "?");

                request.RequestUri = new Uri(url);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
