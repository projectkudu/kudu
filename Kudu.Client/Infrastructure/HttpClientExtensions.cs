using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

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

        public static void SetClientCredentials(this HttpClient client, ICredentials credentials)
        {
            NetworkCredential networkCred = credentials.GetCredential(client.BaseAddress, "Basic");
            string credParameter = Convert.ToBase64String(Encoding.ASCII.GetBytes(networkCred.UserName + ":" + networkCred.Password));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credParameter);
        }
    }
}
