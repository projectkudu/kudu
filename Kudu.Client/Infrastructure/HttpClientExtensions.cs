using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
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

        public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
        {
            HttpResponseMessage result = await client.GetAsync(url);

            string content = await result.EnsureSuccessful().Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client)
        {
            using (var stringContent = new StringContent(String.Empty))
            {
                HttpResponseMessage result = await client.PostAsync(String.Empty, stringContent);
                return result.EnsureSuccessful();
            }
        }

        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client, string requestUri)
        {
            using (var stringContent = new StringContent(String.Empty))
            {
                HttpResponseMessage result = await client.PostAsync(requestUri, stringContent);
                return result.EnsureSuccessful();
            }
        }

        public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, string requestUri, bool ensureSuccessful = true)
        {
            using (var stringContent = new StringContent(String.Empty))
            {
                HttpResponseMessage result = await client.PutAsync(requestUri, stringContent);
                return ensureSuccessful ? result.EnsureSuccessful() : result;
            }
        }

        public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, string requestUri, params KeyValuePair<string, string>[] items)
        {
            using (var jsonContent = HttpClientHelper.CreateJsonContent(items))
            {
                HttpResponseMessage result = await client.PutAsync(requestUri, jsonContent);
                return result.EnsureSuccessful();
            }
        }

        public static async Task<HttpResponseMessage> DeleteSafeAsync(this HttpClient client, string requestUri)
        {
            HttpResponseMessage result = await client.DeleteAsync(requestUri);
            return result.EnsureSuccessful();
        }

        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client, string url, params KeyValuePair<string, string>[] items)
        {
            using (var jsonContent = HttpClientHelper.CreateJsonContent(items))
            {
                HttpResponseMessage result = await client.PostAsync(url, jsonContent);
                return result.EnsureSuccessful();
            }
        }

        public static async Task<TOutput> PostJsonAsync<TInput, TOutput>(this HttpClient client, string url, TInput param)
        {
            HttpResponseMessage result = await client.PostAsJsonAsync(url, param);

            string content = await result.EnsureSuccessful().Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<TOutput>(content);
        }

        public static async Task<TOutput> PutJsonAsync<TInput, TOutput>(this HttpClient client, string url, TInput param)
        {
            HttpResponseMessage result = await client.PutAsJsonAsync(url, param);

            string content = await result.EnsureSuccessful().Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<TOutput>(content);
        }
    }
}
