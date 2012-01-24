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
        public static T GetAsyncJson<T>(this HttpClient client, string url)
        {
            var response = client.GetAsync(url);
            var content = response.Result.EnsureSuccessful().Content.ReadAsStringAsync().Result;
            
            return JsonConvert.DeserializeObject<T>(content);
        }

        public static HttpResponseMessage EnsureSuccessful(this HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().Result;
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
