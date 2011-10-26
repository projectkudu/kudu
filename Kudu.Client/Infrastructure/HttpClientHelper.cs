using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using Kudu.Core.Infrastructure;

namespace Kudu.Client.Infrastructure
{
    internal static class HttpClientHelper
    {
        public static HttpClient Create(string url)
        {
            // The URL needs to end with a slash for HttpClient to do the right thing with relative paths
            url = UrlUtility.EnsureTrailingSlash(url);

            var client = new HttpClient()
            {
                BaseAddress = new Uri(url),
                MaxResponseContentBufferSize = 30 * 1024 * 1024
            };

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static HttpContent CreateJsonContent(params KeyValuePair<string, object>[] items)
        {
            var jsonObject = new SimpleJson.JsonObject();
            foreach (KeyValuePair<string, object> kv in items)
            {
                jsonObject.Add(kv);
            }
            return new ObjectContent(typeof(SimpleJson.JsonObject), jsonObject, "application/json", SimpleJsonFormatter);
        }

        private static IEnumerable<MediaTypeFormatter> SimpleJsonFormatter = new MediaTypeFormatter[] { new SimpleJsonMediaTypeFormatter() };
    }
}
