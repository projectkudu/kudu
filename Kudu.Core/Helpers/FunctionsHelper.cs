using System;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Helpers
{
    public static class FunctionsHelper
    {
        private static HttpClient _httpClient;

        internal static HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                }
                return _httpClient;
            }
            set
            {
                _httpClient = value;
            }
        }

        public static async Task SyncTriggersAsync(ITracer tracer)
        {
            string uri = GetRuntimeUri("admin/host/synctriggers");

            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            await SendRuntimeRequestAsync(request, tracer);
        }

        private static async Task<HttpResponseMessage> SendRuntimeRequestAsync(HttpRequestMessage request, ITracer tracer)
        {
            try
            {
                var requestId = Guid.NewGuid().ToString();
                tracer.Trace($"Issuing request to runtime (Method: {request.Method}, Uri: {request.RequestUri}, RequestId: {requestId})");

                request.Headers.UserAgent.Add(PostDeploymentHelper.UserAgent.Value);
                request.Headers.Add(Constants.SiteRestrictedToken, SimpleWebTokenHelper.CreateToken(DateTime.UtcNow.AddMinutes(5)));
                request.Headers.Add(Constants.RequestIdHeader, requestId);

                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                tracer.Trace($"Request succeeded (Status: {response.StatusCode})");

                return response;
            }
            catch (HttpRequestException ex)
            {
                tracer.TraceError(ex, "Request failed");
                throw;
            }
        }

        private static string GetRuntimeUri(string path)
        {
            // form the runtime host name from our own SCM host
            // by removing the scm segement
            var host = PostDeploymentHelper.HttpHost;
            host = host.Replace(".scm.", ".");

            return $"https://{host}/{path}";
        }
    }
}
