using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Kudu.Core.Scaling
{
    public static class AzureTableUtils
    {
        private static Lazy<ProductInfoHeaderValue> _userAgent = new Lazy<ProductInfoHeaderValue>(() =>
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var version = FileVersionInfo.GetVersionInfo(location);
            return new ProductInfoHeaderValue("kudu", version.FileVersion);
        });

        private static string _protocol;
        private static string _storageAccount;
        private static string _storageKey;
        private static HttpClient _azureTableClient;

        public static HttpClient HttpClient
        {
            get
            {
                if (_azureTableClient == null)
                {
                    HttpClient httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata=nometadata");
                    httpClient.DefaultRequestHeaders.Add("x-ms-version", "2015-04-05");
                    httpClient.DefaultRequestHeaders.Add("MaxDataServiceVersion", "3.0;NetFx");
                    httpClient.DefaultRequestHeaders.Add("DataServiceVersion", "1.0;NetFx");

                    _azureTableClient = httpClient;
                }

                return _azureTableClient;
            }
            set
            {
                _azureTableClient = value;
            }
        }

        public static string Protocol
        {
            get
            {
                if (_protocol == null)
                {
                    Initialize();
                }

                return _protocol;
            }
            set
            {
                _protocol = value;
            }
        }

        public static string StorageAccount
        {
            get
            {
                if (_storageAccount == null)
                {
                    Initialize();
                }

                return _storageAccount;
            }
            set
            {
                _storageAccount = value;
            }
        }

        public static string StorageKey
        {
            get
            {
                if (_storageKey == null)
                {
                    Initialize();
                }

                return _storageKey;
            }
            set
            {
                _storageKey = value;
            }
        }

        public static async Task<IEnumerable<string>> ListTables()
        {
            Uri uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/Tables", Protocol, StorageAccount));
            using (var response = await HttpInvoke(HttpMethod.Get, uri))
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AzureTableResult<AzureTableInfo>>(json)?.Value?.Select(t => t.TableName);
            }
        }

        public static async Task<IEnumerable<AzureWorkerInfo>> ListWorkers(string partitionKey = null)
        {
            var uriBuild = new UriBuilder(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable()", Protocol, StorageAccount));
            if (partitionKey != null)
            {
                uriBuild.Query = string.Format("$filter=PartitionKey eq '{0}'", partitionKey);
            }

            using (var response = await HttpInvoke(HttpMethod.Get, uriBuild.Uri))
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AzureTableResult<AzureWorkerInfo>>(json)?.Value;
            }
        }

        public static async Task<AzureWorkerInfo> GetWorker(string partitionKey, string stampName = null, string workerName = null)
        {
            string rowKey;
            if (partitionKey.IndexOf('(') <= 0)
            {
                rowKey = string.Format("{0}:{1}", stampName, workerName);
            }
            else
            {
                rowKey = partitionKey.Split('(').First();
            }

            var uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable(PartitionKey='{2}',RowKey='{3}')", Protocol, StorageAccount, partitionKey, rowKey));
            using (var response = await HttpInvoke(HttpMethod.Get, uri))
            {
                string json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AzureWorkerInfo>(json);
            }
        }

        public static async Task UpdateWorker(string partitionKey, string stampName, string workerName, int loadFactor)
        {
            string rowKey;
            if (partitionKey.IndexOf('(') <= 0)
            {
                rowKey = string.Format("{0}:{1}", stampName, workerName);
            }
            else
            {
                rowKey = partitionKey.Split('(').First();
            }

            var uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable(PartitionKey='{2}',RowKey='{3}')", Protocol, StorageAccount, partitionKey, rowKey));

            var info = new AzureWorkerInfo()
            {
                StampName = stampName,
                WorkerName = workerName,
                LoadFactor = loadFactor
            };

            var content = new StringContent(JsonConvert.SerializeObject(info), Encoding.UTF8, "application/json");
            using (await HttpInvoke(HttpMethod.Put, uri, content))
            {
                // no-op
            }
        }

        public static async Task InsertWorker(string partitionKey, string stampName, string workerName, int loadFactor)
        {
            string rowKey;
            if (partitionKey.IndexOf('(') <= 0)
            {
                rowKey = string.Format("{0}:{1}", stampName, workerName);
            }
            else
            {
                rowKey = partitionKey.Split('(').First();
            }

            var uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable", Protocol, StorageAccount));

            var info = new AzureWorkerInfo()
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                StampName = stampName,
                WorkerName = workerName,
                LoadFactor = loadFactor
            };

            var content = new StringContent(JsonConvert.SerializeObject(info), Encoding.UTF8, "application/json");
            using (await HttpInvoke(HttpMethod.Post, uri, content))
            {
                // no-op
            }
        }

        public static async Task DeleteWorker(string partitionKey, string stampName = null, string workerName = null)
        {
            string rowKey;
            if (partitionKey.IndexOf('(') <= 0)
            {
                rowKey = string.Format("{0}:{1}", stampName, workerName);
            }
            else
            {
                rowKey = partitionKey.Split('(').First();
            }

            var uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable(PartitionKey='{2}',RowKey='{3}')", Protocol, StorageAccount, partitionKey, rowKey));

            using (await HttpInvoke(HttpMethod.Delete, uri))
            {
                // no-op
            }
        }

        private static void Initialize()
        {
            // TODO, suwatch: remove
            var dictionary = (System.Environment.GetEnvironmentVariable("AzureWebJobsDashboard") ?? "DefaultEndpointsProtocol=https;AccountName=antfunctions;AccountKey=XVYaUffMUpRPldbS9EmMNZ8+SsD+O+Gm1BFLEuFkekFNRTy/DDl7t9fpkEuPO3u3t5UjrdVPa+kR0y8A4bzttQ==")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split(new[] { '=' }, 2))
                .ToDictionary(p => p[0], p => p[1]);

            _protocol = dictionary["DefaultEndpointsProtocol"];
            _storageAccount = dictionary["AccountName"];
            _storageKey = dictionary["AccountKey"];
        }

        private static async Task<HttpResponseMessage> HttpInvoke(HttpMethod method, Uri uri, HttpContent content = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, uri);

            if (content != null)
            {
                request.Content = content;
            }

            if (method == HttpMethod.Put || method == HttpMethod.Delete)
            {
                request.Headers.Add("If-Match", "*");
            }

            var date = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
            request.Headers.Add("x-ms-date", date);
            request.Headers.Authorization = CreateAuthorizationHeader(date, uri.AbsolutePath);

            var response = await HttpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            AzureODataError odataError = null;
            try
            {
                odataError = JsonConvert.DeserializeObject<AzureODataError>(await response.Content.ReadAsStringAsync());
                return response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                if (odataError != null && odataError.Error != null && odataError.Error.Message != null)
                {
                    throw new HttpRequestException(string.Format("{0} {1}", odataError.Error.Code, odataError.Error.Message.Value), ex);
                }

                throw;
            }
        }

        private static AuthenticationHeaderValue CreateAuthorizationHeader(string date, string resourcePath)
        {
            var canonicalizedString = string.Format("{0}\n/{1}{2}", date, StorageAccount, resourcePath);
            var bytes = Encoding.UTF8.GetBytes(canonicalizedString);
            using (var hmacshA256 = new HMACSHA256(Convert.FromBase64String(StorageKey)))
            {
                return new AuthenticationHeaderValue("SharedKeyLite", string.Format("{0}:{1}", StorageAccount, Convert.ToBase64String(hmacshA256.ComputeHash(bytes))));
            }
        }

        public class AzureODataError
        {
            [JsonProperty(PropertyName = "odata.error")]
            public AzureTableUtils.AzureODataError.ODataError Error { get; set; }

            public class ODataError
            {
                [JsonProperty(PropertyName = "code")]
                public string Code { get; set; }

                [JsonProperty(PropertyName = "message")]
                public AzureTableUtils.AzureODataError.ODataMessage Message { get; set; }
            }

            public class ODataMessage
            {
                [JsonProperty(PropertyName = "lang")]
                public string Language { get; set; }

                [JsonProperty(PropertyName = "value")]
                public string Value { get; set; }
            }
        }

        public class AzureTableResult<T>
        {
            [JsonProperty(PropertyName = "value")]
            public IEnumerable<T> Value { get; set; }
        }

        public class AzureTableInfo
        {
            [JsonProperty(PropertyName = "TableName")]
            public string TableName { get; set; }
        }

        public class AzureWorkerInfo
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, PropertyName = "PartitionKey")]
            public string PartitionKey { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, PropertyName = "RowKey")]
            public string RowKey { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, PropertyName = "Timestamp")]
            public string Timestamp { get; set; }

            [JsonProperty(PropertyName = "StampName")]
            public string StampName { get; set; }

            [JsonProperty(PropertyName = "WorkerName")]
            public string WorkerName { get; set; }

            [JsonProperty(PropertyName = "LoadFactor")]
            public int LoadFactor { get; set; }
        }
    }
}
