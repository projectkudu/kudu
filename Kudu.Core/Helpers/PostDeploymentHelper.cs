using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Kudu.Core.Helpers
{
    public static class PostDeploymentHelper
    {
        public const string AutoSwapLockFile = "autoswap.lock";

        private static Lazy<ProductInfoHeaderValue> _userAgent = new Lazy<ProductInfoHeaderValue>(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return new ProductInfoHeaderValue("kudu", fvi.FileVersion);
        });

        private static TraceListener _tracer;
        private static Func<HttpClient> _httpClientFactory;

        // for mocking or override behavior
        public static Func<HttpClient> HttpClientFactory
        {
            get {  return _httpClientFactory ?? new Func<HttpClient>(() => new HttpClient()); }
            set { _httpClientFactory = value; }
        }

        private static string AutoSwapLockFilePath
        {
            get { return System.Environment.ExpandEnvironmentVariables(@"%HOME%\site\locks\" + AutoSwapLockFile); }
        }

        // HTTP_HOST = site.scm.azurewebsites.net
        private static string HttpHost
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.HttpHost); }
        }

        // X-MS-SITE-RESTRICTED-JWT = this is jwt literal
        private static string SiteRestrictedJWT
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.SiteRestrictedJWT); }
        }

        // WEBSITE_SWAP_SLOTNAME = Production
        private static string WebSiteSwapSlotName
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.WebSiteSwapSlotName); }
        }

        // x-ms-request-id = guid
        private static string RequestIdHeader
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.RequestIdHeader); }
        }

        // FUNCTIONS_EXTENSION_VERSION = ~1.0
        private static string FunctionRunTimeVersion
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion); }
        }

        // ROUTING_EXTENSION_VERSION = 1.0
        private static string RoutingRunTimeVersion
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.RoutingRunTimeVersion); }
        }

        /// <summary>
        /// This common codes is to invoke post deployment operations.
        /// It is written to require least dependencies but framework assemblies.
        /// Caller is responsible for synchronization.
        /// </summary>
        public static async Task Run(TraceListener tracer)
        {
            await SyncFunctionsTriggers(tracer);

            await PerformAutoSwap(tracer);
        }

        public static async Task SyncFunctionsTriggers(TraceListener tracer)
        {
            _tracer = tracer;

            if (string.IsNullOrEmpty(FunctionRunTimeVersion))
            {
                Trace(TraceEventType.Verbose, "Functions are not enabled");
                return;
            }

            VerifyEnvironments();

            // use framework serializer to avoid dependency requirement on callers
            // though it is not the best serializer, it should do for this specific use.
            var serializer = new JavaScriptSerializer();
            var funtionsPath = System.Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot");
            var triggers = Directory
                    .GetDirectories(funtionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(File.Exists)
                    .SelectMany(f => DeserializeFunctionTrigger(serializer, f))
                    .ToList();

            if (!string.IsNullOrEmpty(RoutingRunTimeVersion))
            {
                triggers.Add(new Dictionary<string, object> { { "type", "routingTrigger" } });
            }

            var content = serializer.Serialize(triggers);
            Exception exception = null;
            try
            {
                await PostAsync("/operations/settriggers", content);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                Trace(TraceEventType.Information,
                      "Syncing {0} function triggers with payload size {1} bytes {2}",
                      triggers.Count,
                      content.Length,
                      exception == null ? "successful." : "failed with " + exception.Message);
            }
        }

        public static bool IsAutoSwapOngoing()
        {
            if (string.IsNullOrEmpty(WebSiteSwapSlotName))
            {
                return false;
            }

            // Auto swap is ongoing if the auto swap lock file exists and is written to less than 2 minutes ago
            var fileInfo = new FileInfo(AutoSwapLockFilePath);
            return fileInfo.Exists && fileInfo.LastWriteTimeUtc.AddMinutes(2) >= DateTime.UtcNow;
        }

        public static bool IsAutoSwapEnabled()
        {
            return !string.IsNullOrEmpty(WebSiteSwapSlotName);
        }

        public static async Task PerformAutoSwap(TraceListener tracer)
        {
            _tracer = tracer;

            var slotSwapName = WebSiteSwapSlotName;
            if (string.IsNullOrEmpty(slotSwapName))
            {
                Trace(TraceEventType.Verbose, "AutoSwap is not enabled");
                return;
            }

            VerifyEnvironments();

            var operationId = string.Format("AUTOSWAP{0}", Guid.NewGuid());
            Exception exception = null;
            try
            {
                await PostAsync(string.Format("/operations/autoswap?slot={0}&operationId={1}", slotSwapName, operationId));

                WriteAutoSwapOngoing();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                Trace(TraceEventType.Information, 
                      "Requesting auto swap to '{0}' slot with '{1}' id {2}", 
                      slotSwapName, 
                      operationId,
                      exception == null ? "successful." : "failed with " + exception.Message);
            }
        }

        private static void VerifyEnvironments()
        {
            if (string.IsNullOrEmpty(SiteRestrictedJWT))
            {
                throw new InvalidOperationException(String.Format("Missing {0} env!", Constants.SiteRestrictedJWT));
            }

            if (string.IsNullOrEmpty(HttpHost))
            {
                throw new InvalidOperationException(String.Format("Missing {0} env!", Constants.HttpHost));
            }
        }

        private static IEnumerable<Dictionary<string, object>> DeserializeFunctionTrigger(JavaScriptSerializer serializer, string functionJson)
        {
            try
            {
                var functionName = Path.GetFileName(Path.GetDirectoryName(functionJson));
                var json = (Dictionary<string, object>)serializer.DeserializeObject(File.ReadAllText(functionJson));

                object value;
                var disabled = json.TryGetValue("disabled", out value) && (bool)value;
                if (disabled)
                {
                    Trace(TraceEventType.Verbose, "Function {0} is disabled", functionName);
                    return Enumerable.Empty<Dictionary<string, object>>();
                }

                var excluded = json.TryGetValue("excluded", out value) && (bool)value;
                if (excluded)
                {
                    Trace(TraceEventType.Verbose, "Function {0} is excluded", functionName);
                    return Enumerable.Empty<Dictionary<string, object>>();
                }

                var triggers = new List<Dictionary<string, object>>();
                foreach (Dictionary<string, object> binding in (object[])json["bindings"])
                {
                    var type = (string)binding["type"];
                    if (type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))
                    {
                        binding.Add("functionName", functionName);
                        Trace(TraceEventType.Verbose, "Syncing {0} of {1}", type, functionName);
                        triggers.Add(binding);
                    }
                    else
                    {
                        Trace(TraceEventType.Verbose, "Skipping {0} of {1}", type, functionName);
                    }
                }

                return triggers;
            }
            catch (Exception ex)
            {
                Trace(TraceEventType.Warning, "{0} is invalid. {1}", functionJson, ex.Message);
            }

            return Enumerable.Empty<Dictionary<string, object>>();
        }

        private static void WriteAutoSwapOngoing()
        {
            var autoSwapLockFilePath = AutoSwapLockFilePath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(autoSwapLockFilePath));
                File.WriteAllText(autoSwapLockFilePath, string.Empty);
            }
            catch (Exception ex)
            {
                // best effort
                Trace(TraceEventType.Warning, "Fail to write {0}.  {1}", autoSwapLockFilePath, ex);
            }
        }

        private static async Task PostAsync(string path, string content = null)
        {
            var host = HttpHost;
            var requestId = RequestIdHeader ?? Guid.NewGuid().ToString();
            var jwt = SiteRestrictedJWT;

            var statusCode = default(HttpStatusCode);
            Trace(TraceEventType.Verbose, "Begin HttpPost https://{0}{1}, x-ms-request-id: {2}", host, path, requestId);
            try
            {
                using (var client = HttpClientFactory())
                {
                    client.BaseAddress = new Uri(string.Format("https://{0}", host));
                    client.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                    client.DefaultRequestHeaders.Add(Constants.RequestIdHeader, requestId);

                    var payload = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/json");
                    using (var response = await client.PostAsync(path, payload))
                    {
                        statusCode = response.StatusCode;
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
            finally
            {
                Trace(TraceEventType.Verbose, "End HttpPost, status: {0}", statusCode);
            }
        }

        private static void Trace(TraceEventType eventType, string message)
        {
            Trace(eventType, "{0}", message);
        }

        private static void Trace(TraceEventType eventType, string format, params object[] args)
        {
            var tracer = _tracer;
            if (tracer != null)
            {
                tracer.TraceEvent(null, "PostDeployment", eventType, (int)eventType, format, args);
            }
        }
    }
}
