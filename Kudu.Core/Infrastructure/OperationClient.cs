using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
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
            if (String.IsNullOrEmpty(jwt))
            {
                throw new InvalidOperationException("Missing HTTP_HOST env!");
            }

            using (_tracer.Step("POST " + path))
            {
                using (var client = ClientHandler != null ? new HttpClient(ClientHandler) : new HttpClient())
                {
                    client.BaseAddress = new Uri("https://" + host);
                    client.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

                    HttpResponseMessage response = await client.PostAsJsonAsync(path, content);
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