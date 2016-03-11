using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public class OperationClient
    {
        // For testing purpose
        public static HttpMessageHandler ClientHandler { get; set; }

        private static Lazy<ProductInfoHeaderValue> _userAgent = new Lazy<ProductInfoHeaderValue>(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return new ProductInfoHeaderValue("kudu", fvi.FileVersion);
        });

        private readonly ITracer _tracer;

        public OperationClient(ITracer tracer)
        {
            _tracer = tracer;
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string path, T content = default(T))
        {
            var jwt = System.Environment.GetEnvironmentVariable(Constants.SiteRestrictedJWT);
            if (String.IsNullOrEmpty(jwt))
            {
                throw new InvalidOperationException(String.Format("Missing {0} header!", Constants.SiteRestrictedJWT));
            }

            var host = System.Environment.GetEnvironmentVariable(Constants.HttpHost);
            if (String.IsNullOrEmpty(host))
            {
                throw new InvalidOperationException("Missing HTTP_HOST env!");
            }

            var requestId = HttpContext.Current?.Request?.Headers?[Constants.RequestIdHeader];
            requestId = !String.IsNullOrEmpty(requestId) ? requestId : Guid.NewGuid().ToString();

            using (_tracer.Step($"POST {path}, x-ms-request-id: {requestId}"))
            {
                using (var client = ClientHandler != null ? new HttpClient(ClientHandler) : new HttpClient())
                {
                    client.BaseAddress = new Uri($"https://{host}");
                    client.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                    client.DefaultRequestHeaders.Add(Constants.RequestIdHeader, requestId);

                    HttpResponseMessage response = await client.PostAsJsonAsync(path, content);
                    _tracer.Trace("Response: " + response.StatusCode);
                    response.EnsureSuccessStatusCode();
                    return response;
                }
            }
        }

        public static void SkipSslValidationIfNeeded()
        {
            if (System.Environment.GetEnvironmentVariable(SettingsKeys.SkipSslValidation) == "1")
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            }
        }
    }
}