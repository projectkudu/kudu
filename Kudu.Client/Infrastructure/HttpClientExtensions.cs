using System;
using System.Net.Http;
using Newtonsoft.Json;

namespace Kudu.Client.Infrastructure
{
    public static class HttpClientExtensions
    {
        public static T GetJson<T>(this HttpClient client, string url)
        {
            HttpResponseMessage response = client.Get(url);
            var content = response.EnsureSuccessful().Content.ReadAsString();

            return JsonConvert.DeserializeObject<T>(content);
        }

        public static HttpResponseMessage EnsureSuccessful(this HttpResponseMessage response)
        {
            var content = response.Content.ReadAsString();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(content);
            }

            return response;
        }
    }
}
