using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web.Security;
using Microsoft.Win32;

namespace Kudu.Core.Scaling
{
    public class ScaleManager : IScaleManager
    {
        public const string Purpose = "ScaleManager";
        public const int HttpTimeoutSeconds = 60;

        public const string WorkerNotFound = "Worker Not Found";
        public const string SiteUnavailableFromMiniArr = "Site Unavailable from Mini-ARR";
        public const string NoCapacity = "No Capacity";
        public const string ScaleNotAllowed = "Scale Not Allowed";

        private static readonly string _partitionKey;
        private static readonly string _managerKey;

        private static readonly TimeSpan TokenValidity = TimeSpan.FromHours(1);
        private static string _token;
        private static string _workerName;
        private static DateTime _tokenExpiredUtc = DateTime.MinValue;

        private static HttpClient _httpClient;

        private static Lazy<ProductInfoHeaderValue> _userAgent = new Lazy<ProductInfoHeaderValue>(() =>
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var version = FileVersionInfo.GetVersionInfo(location);
            return new ProductInfoHeaderValue("kudu", version.FileVersion);
        });

        static ScaleManager()
        {
            // TODO, suwatch: remove
            string runtimeSiteName = System.Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME") ?? "functiondev200";
            if (runtimeSiteName.StartsWith("~1", StringComparison.OrdinalIgnoreCase))
            {
                runtimeSiteName = runtimeSiteName.Substring(2);
            }

            _partitionKey = runtimeSiteName;
            _managerKey = string.Format("{0}(manager)", runtimeSiteName);
        }

        public static string WorkerName
        {
            get
            {
                if (_workerName == null)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\IIS Extensions\DwasMod"))
                    {
                        _workerName = ((string)key?.GetValue("IpAddress"))?.ToLowerInvariant();
                    }
                }

                return _workerName;
            }

            set
            {
                _workerName = value;
            }
        }

        public async Task<IEnumerable<WorkerInfo>> ListWorkers()
        {
            var workers = await AzureTableUtils.ListWorkers();
            var manager = workers.FirstOrDefault(w =>
            {
                return string.Equals(w.PartitionKey, _managerKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(w.RowKey, _partitionKey, StringComparison.OrdinalIgnoreCase);
            });

            return workers
                .Where(w => string.Equals(w.PartitionKey, _partitionKey, StringComparison.OrdinalIgnoreCase))
                .Select(w => new WorkerInfo
                {
                    Id = w.RowKey,
                    StampName = w.StampName,
                    WorkerName = w.WorkerName,
                    LoadFactor = w.LoadFactor >= int.MaxValue ? "MAX" : (w.LoadFactor <= int.MinValue ? "MIN" : w.LoadFactor.ToString()),
                    LastModifiedTimeUtc = w.Timestamp,
                    IsManager = manager != null && string.Equals(w.StampName, manager.StampName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(w.WorkerName, manager.WorkerName, StringComparison.OrdinalIgnoreCase),
                    IsStale = DateTime.UtcNow > DateTime.Parse(w.Timestamp).ToUniversalTime().AddSeconds(120)
                });
        }

        public async Task<WorkerInfo> GetWorker(string id)
        {
            return (await ListWorkers()).First(w => w.Id == id);
        }

        public async Task UpdateWorker(string id, WorkerInfo info)
        {
            var parts = id.ToLowerInvariant().Split(':');
            var stampName = parts[0];
            var workerName = parts[1];

            if (workerName == "127.0.0.1")
            {
                workerName = WorkerName ?? workerName;
            }

            int loadFactor = 0;
            if (info != null && !int.TryParse(info.LoadFactor, out loadFactor))
            {
                if (string.Equals(info.LoadFactor, "max", StringComparison.OrdinalIgnoreCase))
                {
                    loadFactor = int.MaxValue;
                }
                else if (string.Equals(info.LoadFactor, "min", StringComparison.OrdinalIgnoreCase))
                {
                    loadFactor = int.MinValue;
                }
            }

            try
            {
                await AzureTableUtils.UpdateWorker(_partitionKey, stampName, workerName, loadFactor);
                return;
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("ResourceNotFound") < 0)
                {
                    throw;
                }
            }

            await AzureTableUtils.InsertWorker(_partitionKey, stampName, workerName, loadFactor);
        }

        public async Task<HttpResponseMessage> PingWorker(string id)
        {
            var parts = id.ToLowerInvariant().Split(':');
            var stampName = parts[0];
            var workerName = parts[1];

            var stampHostName = GetStampHostName(stampName);
            var pathAndQuery = string.Format("https://{0}/operations/keepalive/{1}/{2}?token={3}",
                stampHostName,
                _partitionKey,
                workerName,
                GetToken());

            // TODO, suwatch: remove
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME")))
            {
                pathAndQuery = string.Format("https://requestb.in/1010i831?operations=keepalive-{1}-{2}&token={3}",
                    stampHostName,
                    _partitionKey,
                    workerName,
                    GetToken());
            }

            return await SendAsync(HttpMethod.Get, pathAndQuery);
        }

        public async Task<HttpResponseMessage> AddWorker(string id)
        {
            var parts = id.ToLowerInvariant().Split(':');
            var stampName = parts[0];

            var stampHostName = GetStampHostName(stampName);
            var pathAndQuery = string.Format("https://{0}/operations/addworker/{1}?token={2}&workers={3}",
                stampHostName,
                _partitionKey,
                GetToken(),
                1);

            // TODO, suwatch: remove
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME")))
            {
                pathAndQuery = string.Format("https://requestb.in/1010i831?operations=addworker-{1}&token={2}&workers={3}",
                stampHostName,
                _partitionKey,
                GetToken(),
                1);
            }

            return await SendAsync(HttpMethod.Post, pathAndQuery);
        }

        public async Task<HttpResponseMessage> RemoveWorker(string id)
        {
            var parts = id.ToLowerInvariant().Split(':');
            var stampName = parts[0];
            var workerName = parts[1];

            var stampHostName = GetStampHostName(stampName);
            var pathAndQuery = string.Format("https://{0}/operations/removeworker/{1}/{2}?token={3}",
                stampHostName,
                _partitionKey,
                workerName,
                GetToken());

            // TODO, suwatch: remove
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WEBSITE_IIS_SITE_NAME")))
            {
                pathAndQuery = string.Format("https://requestb.in/1010i831?operations=removeworker-{1}-{2}&token={3}",
                stampHostName,
                _partitionKey,
                workerName,
                GetToken());
            }

            return await SendAsync(HttpMethod.Delete, pathAndQuery);
        }

        private static async Task<HttpResponseMessage> SendAsync(HttpMethod method, string pathAndQuery)
        {
            var client = GetHttpClient();
            var request = new HttpRequestMessage(method, pathAndQuery);
            return await client.SendAsync(request);
        }

        private static HttpClient GetHttpClient()
        {
            if (_httpClient == null)
            {
                var handler = new WebRequestHandler();
                handler.ServerCertificateValidationCallback = ServerCertificateValidation;

                var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);

                var hostName = System.Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(hostName))
                {
                    client.DefaultRequestHeaders.Host = hostName;
                }

                client.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);

                _httpClient = client;
            }

            return _httpClient;
        }

        private static bool ServerCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return (sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) == 0;
        }

        public static string GetStampHostName(string stampName)
        {
            return string.Format("{0}.cloudapp.net", stampName);
        }

        public static string GetToken()
        {
            if (string.IsNullOrEmpty(_token) || _tokenExpiredUtc < DateTime.UtcNow)
            {
                var expiredUtc = DateTime.UtcNow.Add(TokenValidity);
                var token = GetToken(expiredUtc);
                _token = WebUtility.UrlEncode(token);
                _tokenExpiredUtc = expiredUtc;
            }

            return _token;
        }

        public static string GetToken(DateTime expiredUtc)
        {
            var bytes = BitConverter.GetBytes(expiredUtc.Ticks);
            var encrypted = MachineKey.Protect(bytes, Purpose);
            return Convert.ToBase64String(encrypted);
        }
    }
}