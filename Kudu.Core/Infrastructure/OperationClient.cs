using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public class OperationClient
    {
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

        public async Task<HttpResponseMessage> PostAsync(string path)
        {
            return await PostAsync<string>(path);
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string path, T content = default(T))
        {
            var jwt = System.Environment.GetEnvironmentVariable(Constants.X_MS_SITE_RESTRICTED_JWT);
            var host = System.Environment.GetEnvironmentVariable(Constants.HTTP_HOST);
            using (_tracer.Step("POST " + path))
            {
                using (var client = new HttpClient())
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
    }
}