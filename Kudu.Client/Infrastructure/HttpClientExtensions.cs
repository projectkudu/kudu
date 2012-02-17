using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Client.Infrastructure
{
    public static class HttpClientExtensions
    {
        public static T GetJson<T>(this HttpClient client, string url)
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

        public static Task<T> GetJsonAsync<T>(this HttpClient client, string url)
        {
            return client.GetAsync(url).Then<HttpResponseMessage, T>(result =>
            {
                return result.Content.ReadAsStringAsync().Then<string, T>(content =>
                {
                    if (!result.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(content);
                    }

                    return JsonConvert.DeserializeObject<T>(content);
                });
            });
        }

        public static Task PostAsync(this HttpClient client)
        {
            return client.PostAsync(String.Empty, new StringContent(String.Empty)).Then(result =>
            {
                return result.Content.ReadAsStringAsync().Then(content =>
                {
                    if (!result.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(content);
                    }
                });
            });
        }

        public static Task PostAsync(this HttpClient client, string url, KeyValuePair<string, string> param)
        {
            return client.PostAsync(url, HttpClientHelper.CreateJsonContent(param)).Then(result =>
            {
                return result.Content.ReadAsStringAsync().Then(content =>
                {
                    if (!result.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(content);
                    }
                });
            });
        }
    }
}
