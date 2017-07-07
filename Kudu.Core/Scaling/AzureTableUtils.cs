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
                    httpClient.DefaultRequestHeaders.UserAgent.Add(AzureTableUtils._userAgent.Value);
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

        public static async Task<AzureTableUtils.AzureWorkerInfo> GetWorker(string partitionKey, string stampName = null, string workerName = null)
        {
            string str1;
            if (partitionKey.IndexOf('(') <= 0)
                str1 = string.Format("{0}:{1}", (object)stampName, (object)workerName);
            else
                str1 = ((IEnumerable<string>)partitionKey.Split('(')).First<string>();
            string rowKey = str1;
            Uri uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable(PartitionKey='{2}',RowKey='{3}')", new object[4]
            {
        (object) AzureTableUtils.Protocol,
        (object) AzureTableUtils.StorageAccount,
        (object) partitionKey,
        (object) rowKey
            }));
            HttpResponseMessage httpResponseMessage = await AzureTableUtils.HttpInvoke(HttpMethod.Get, uri, (HttpContent)null);
            HttpResponseMessage response = httpResponseMessage;
            httpResponseMessage = (HttpResponseMessage)null;
            AzureTableUtils.AzureWorkerInfo azureWorkerInfo;
            try
            {
                string str = await response.Content.ReadAsStringAsync();
                azureWorkerInfo = JsonConvert.DeserializeObject<AzureTableUtils.AzureWorkerInfo>(str);
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
            return azureWorkerInfo;
        }

        public static async Task UpdateWorker(string partitionKey, string stampName, string workerName, int loadFactor)
        {
            string str;
            if (partitionKey.IndexOf('(') <= 0)
                str = string.Format("{0}:{1}", (object)stampName, (object)workerName);
            else
                str = ((IEnumerable<string>)partitionKey.Split('(')).First<string>();
            string rowKey = str;
            Uri uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable(PartitionKey='{2}',RowKey='{3}')", new object[4]
            {
        (object) AzureTableUtils.Protocol,
        (object) AzureTableUtils.StorageAccount,
        (object) partitionKey,
        (object) (rowKey ?? string.Format("{0}:{1}", (object) stampName, (object) workerName))
            }));
            AzureTableUtils.AzureWorkerInfo info = new AzureTableUtils.AzureWorkerInfo()
            {
                StampName = stampName,
                WorkerName = workerName,
                LoadFactor = loadFactor
            };
            StringContent content = new StringContent(JsonConvert.SerializeObject((object)info), Encoding.UTF8, "application/json");
            HttpResponseMessage httpResponseMessage1 = await AzureTableUtils.HttpInvoke(HttpMethod.Put, uri, (HttpContent)content);
            HttpResponseMessage httpResponseMessage2 = httpResponseMessage1;
            httpResponseMessage1 = (HttpResponseMessage)null;
            try
            {
            }
            finally
            {
                if (httpResponseMessage2 != null)
                    httpResponseMessage2.Dispose();
            }
            httpResponseMessage2 = (HttpResponseMessage)null;
        }

        public static async Task InsertWorker(string partitionKey, string stampName, string workerName, int loadFactor)
        {
            string str;
            if (partitionKey.IndexOf('(') <= 0)
                str = string.Format("{0}:{1}", (object)stampName, (object)workerName);
            else
                str = ((IEnumerable<string>)partitionKey.Split('(')).First<string>();
            string rowKey = str;
            Uri uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable", (object)AzureTableUtils.Protocol, (object)AzureTableUtils.StorageAccount));
            AzureTableUtils.AzureWorkerInfo info = new AzureTableUtils.AzureWorkerInfo()
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                StampName = stampName,
                WorkerName = workerName,
                LoadFactor = loadFactor
            };
            StringContent content = new StringContent(JsonConvert.SerializeObject((object)info), Encoding.UTF8, "application/json");
            HttpResponseMessage httpResponseMessage1 = await AzureTableUtils.HttpInvoke(HttpMethod.Post, uri, (HttpContent)content);
            HttpResponseMessage httpResponseMessage2 = httpResponseMessage1;
            httpResponseMessage1 = (HttpResponseMessage)null;
            try
            {
            }
            finally
            {
                if (httpResponseMessage2 != null)
                    httpResponseMessage2.Dispose();
            }
            httpResponseMessage2 = (HttpResponseMessage)null;
        }

        public static async Task DeleteWorker(string partitionKey, string stampName = null, string workerName = null)
        {
            string str;
            if (partitionKey.IndexOf('(') <= 0)
                str = string.Format("{0}:{1}", (object)stampName, (object)workerName);
            else
                str = ((IEnumerable<string>)partitionKey.Split('(')).First<string>();
            string rowKey = str;
            Uri uri = new Uri(string.Format("{0}://{1}.table.core.windows.net/appserviceworkertable(PartitionKey='{2}',RowKey='{3}')", new object[4]
            {
        (object) AzureTableUtils.Protocol,
        (object) AzureTableUtils.StorageAccount,
        (object) partitionKey,
        (object) rowKey
            }));
            HttpResponseMessage httpResponseMessage = await AzureTableUtils.HttpInvoke(HttpMethod.Delete, uri, (HttpContent)null);
            HttpResponseMessage response = httpResponseMessage;
            httpResponseMessage = (HttpResponseMessage)null;
            try
            {
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
            response = (HttpResponseMessage)null;
        }

        private static void Initialize()
        {
            IEnumerable<string[]> source = ((IEnumerable<string>)(System.Environment.GetEnvironmentVariable("AzureWebJobsDashboard") ?? "DefaultEndpointsProtocol=https;AccountName=antfunctions;AccountKey=XVYaUffMUpRPldbS9EmMNZ8+SsD+O+Gm1BFLEuFkekFNRTy/DDl7t9fpkEuPO3u3t5UjrdVPa+kR0y8A4bzttQ==").Split(new char[1]
            {
        ';'
            }, StringSplitOptions.RemoveEmptyEntries)).Select<string, string[]>((Func<string, string[]>)(p => p.Split(new char[1]
     {
        '='
            }, 2)));
            Func<string[], string> func = (Func<string[], string>)(p => p[0]);
            Func<string[], string> keySelector;
            Dictionary<string, string> dictionary = source.ToDictionary<string[], string, string>(keySelector, (Func<string[], string>)(p => p[1]));
            AzureTableUtils._protocol = dictionary["DefaultEndpointsProtocol"];
            AzureTableUtils._storageAccount = dictionary["AccountName"];
            AzureTableUtils._storageKey = dictionary["AccountKey"];
        }

        private static async Task<HttpResponseMessage> HttpInvoke(HttpMethod method, Uri uri, HttpContent content = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, uri);
            if (content != null)
                request.Content = content;
            if (method == HttpMethod.Put || method == HttpMethod.Delete)
                request.Headers.Add("If-Match", "*");
            string date = DateTime.UtcNow.ToString("R", (IFormatProvider)CultureInfo.InvariantCulture);
            request.Headers.Add("x-ms-date", date);
            request.Headers.Authorization = AzureTableUtils.CreateAuthorizationHeader(date, uri.AbsolutePath);
            HttpClient client = AzureTableUtils.HttpClient;
            HttpResponseMessage httpResponseMessage = await client.SendAsync(request);
            HttpResponseMessage response = httpResponseMessage;
            httpResponseMessage = (HttpResponseMessage)null;
            if (response.IsSuccessStatusCode)
                return response;
            AzureTableUtils.AzureODataError odataError = (AzureTableUtils.AzureODataError)null;
            try
            {
                string str = await response.Content.ReadAsStringAsync();
                odataError = JsonConvert.DeserializeObject<AzureTableUtils.AzureODataError>(str);
                str = (string)null;
                return response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                AzureTableUtils.AzureODataError azureOdataError = odataError;
                string str;
                if (azureOdataError == null)
                {
                    str = (string)null;
                }
                else
                {
                    AzureTableUtils.AzureODataError.ODataError error = azureOdataError.Error;
                    if (error == null)
                    {
                        str = (string)null;
                    }
                    else
                    {
                        AzureTableUtils.AzureODataError.ODataMessage message = error.Message;
                        str = message != null ? message.Value : (string)null;
                    }
                }
                if (!string.IsNullOrEmpty(str))
                    throw new HttpRequestException(string.Format("{0} {1}", (object)odataError.Error.Code, (object)odataError.Error.Message.Value), ex);
                throw;
            }
        }

        private static AuthenticationHeaderValue CreateAuthorizationHeader(string date, string resourcePath)
        {
            string s = string.Format("{0}\n/{1}{2}", (object)date, (object)AzureTableUtils.StorageAccount, (object)resourcePath);
            string empty = string.Empty;
            using (HMACSHA256 hmacshA256 = new HMACSHA256(Convert.FromBase64String(AzureTableUtils.StorageKey)))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(s);
                return new AuthenticationHeaderValue("SharedKeyLite", string.Format("{0}:{1}", (object)AzureTableUtils.StorageAccount, (object)Convert.ToBase64String(hmacshA256.ComputeHash(bytes))));
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
