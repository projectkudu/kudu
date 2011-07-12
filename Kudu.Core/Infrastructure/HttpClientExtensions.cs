using System;
using System.Net.Http;
using Newtonsoft.Json;

namespace Kudu.Core.Infrastructure {
    public static class HttpClientExtensions {
        public static T GetJson<T>(this HttpClient client, string url) {
            var message = client.Get(url);
            var content = message.Content.ReadAsString();

            if (!message.IsSuccessStatusCode) {
                throw new InvalidOperationException(content);
            }

            return JsonConvert.DeserializeObject<T>(content);
        }
    }
}
