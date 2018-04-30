using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            get { return _httpClientFactory ?? new Func<HttpClient>(() => new HttpClient()); }
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

        // WEBSITE_SWAP_SLOTNAME = Production
        private static string WebSiteSwapSlotName
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.WebSiteSwapSlotName); }
        }

        // FUNCTIONS_EXTENSION_VERSION = ~1.0
        private static string FunctionRunTimeVersion
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion); }
        }

        // LOGICAPP_URL = [url to PUT logicapp.json to]
        private static string LogicAppUrl
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.LogicAppUrlKey); }
        }

        // %HOME%\site\wwwroot\logicapp.json
        private static string LogicAppJsonFilePath
        {
            get { return System.Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\" + Constants.LogicAppJson); }
        }

        // WEBSITE_SKU = Dynamic
        private static string WebSiteSku
        {
            get { return System.Environment.GetEnvironmentVariable(Constants.WebSiteSku); }
        }

        // WEBSITE_INSTANCE_ID not null or empty
        public static bool IsAzureEnvironment()
        {
            return !String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        }

        // WEBSITE_HOME_STAMPNAME = waws-prod-bay-001
        private static string HomeStamp
        {
            get { return System.Environment.GetEnvironmentVariable("WEBSITE_HOME_STAMPNAME"); }
        }

        /// <summary>
        /// This common codes is to invoke post deployment operations.
        /// It is written to require least dependencies but framework assemblies.
        /// Caller is responsible for synchronization.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1801:Parameter 'siteRestrictedJwt' is never used", 
            Justification = "Method signature has to be the same because it's called via reflections from web-deploy")]
        public static async Task Run(string requestId, string siteRestrictedJwt, TraceListener tracer)
        {
            await Invoke(requestId, tracer);
        }

        /// <summary>
        /// This common codes is to invoke post deployment operations.
        /// It is written to require least dependencies but framework assemblies.
        /// Caller is responsible for synchronization.
        /// </summary>
        public static async Task Invoke(string requestId, TraceListener tracer)
        {
            RunPostDeploymentScripts(tracer);

            await SyncFunctionsTriggers(requestId, tracer);

            await PerformAutoSwap(requestId, tracer);
        }

        public static async Task SyncFunctionsTriggers(string requestId, TraceListener tracer, string functionsPath = null)
        {
            _tracer = tracer;

            if (string.IsNullOrEmpty(FunctionRunTimeVersion))
            {
                Trace(TraceEventType.Verbose, "Skip function trigger and logicapp sync because function is not enabled.");
                return;
            }

            if (!string.Equals(Constants.DynamicSku, WebSiteSku, StringComparison.OrdinalIgnoreCase))
            {
                Trace(TraceEventType.Verbose, string.Format("Skip function trigger and logicapp sync because sku ({0}) is not dynamic (consumption plan).", WebSiteSku));
                return;
            }

            VerifyEnvironments();

            functionsPath = !string.IsNullOrEmpty(functionsPath)
                ? functionsPath
                : System.Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot");

            // Read host.json
            // Get HubName property for Durable Functions
            Dictionary<string, string> durableConfig = null;
            string hostJson = Path.Combine(functionsPath, Constants.FunctionsHostConfigFile);
            if (File.Exists(hostJson))
            {
                ReadDurableConfig(hostJson, out durableConfig);
            }

            // Collect each functions.json
            var triggers = Directory
                    .GetDirectories(functionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(File.Exists)
                    .SelectMany(f => DeserializeFunctionTrigger(f))
                    .ToList();

            if (File.Exists(Path.Combine(functionsPath, Constants.ProxyConfigFile)))
            {
                var routing = new JObject();
                routing["type"] = "routingTrigger";
                triggers.Add(routing);
            }

            // Add hubName, connection, to each Durable Functions trigger
            if (durableConfig != null)
            {
                foreach (var trigger in triggers)
                {
                    JToken typeValue;
                    if (trigger.TryGetValue("type", out typeValue)
                    && typeValue != null
                    && (typeValue.ToString().Equals("orchestrationTrigger", StringComparison.OrdinalIgnoreCase)
                    || typeValue.ToString().Equals("activityTrigger", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (durableConfig.ContainsKey(Constants.HubName))
                        {
                            trigger["taskHubName"] = durableConfig[Constants.HubName];
                        }

                        if (durableConfig.ContainsKey(Constants.DurableTaskStorageConnection))
                        {
                            trigger[Constants.DurableTaskStorageConnection] = durableConfig[Constants.DurableTaskStorageConnection];
                        }
                    }
                }
            }

            var content = JsonConvert.SerializeObject(triggers);
            Exception exception = null;
            try
            {
                await PostAsync("/operations/settriggers", requestId, content);
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
                      exception == null ? "successful." : ("failed with " + exception));
            }

            // this couples with sync function triggers
            await SyncLogicAppJson(requestId, tracer);
        }

        public static async Task SyncLogicAppJson(string requestId, TraceListener tracer)
        {
            _tracer = tracer;

            var logicAppUrl = LogicAppUrl;
            if (string.IsNullOrEmpty(logicAppUrl))
            {
                return;
            }

            var fileInfo = new FileInfo(LogicAppJsonFilePath);
            if (!fileInfo.Exists)
            {
                Trace(TraceEventType.Verbose, "File {0} does not exists", fileInfo.FullName);
                return;
            }

            var displayUrl = logicAppUrl;
            var queryIndex = logicAppUrl.IndexOf('?');
            if (queryIndex > 0)
            {
                // for display/logging, strip out querystring secret
                displayUrl = logicAppUrl.Substring(0, queryIndex);
            }

            var content = File.ReadAllText(fileInfo.FullName);
            var statusCode = default(HttpStatusCode);
            Exception exception = null;
            try
            {
                Trace(TraceEventType.Verbose, "Begin HttpPut {0}, x-ms-client-request-id: {1}", displayUrl, requestId);

                using (var client = HttpClientFactory())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);
                    client.DefaultRequestHeaders.Add(Constants.ClientRequestIdHeader, requestId);

                    var payload = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/json");
                    using (var response = await client.PutAsync(logicAppUrl, payload))
                    {
                        statusCode = response.StatusCode;
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                Trace(TraceEventType.Verbose, "End HttpPut, status: {0}", statusCode);

                Trace(TraceEventType.Information,
                      "Syncing logicapp {0} with payload size {1} bytes {2}",
                      displayUrl,
                      content.Length,
                      exception == null ? "successful." : ("failed with " + exception));
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

        public static async Task PerformAutoSwap(string requestId, TraceListener tracer)
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
                await PostAsync(string.Format("/operations/autoswap?slot={0}&operationId={1}", slotSwapName, operationId), requestId);

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
                      exception == null ? "successful." : ("failed with " + exception));
            }
        }

        private static void VerifyEnvironments()
        {
            if (string.IsNullOrEmpty(HttpHost))
            {
                throw new InvalidOperationException(String.Format("Missing {0} env!", Constants.HttpHost));
            }
        }

        private static void ReadDurableConfig(string hostConfigPath, out Dictionary<string, string> config)
        {
            config = new Dictionary<string, string>();
            var json = JObject.Parse(File.ReadAllText(hostConfigPath));
            JToken durableTaskValue;

            // we will allow case insensitivity given it is likely user hand edited
            // see https://github.com/Azure/azure-functions-durable-extension/issues/111
            if (json.TryGetValue(Constants.DurableTask, StringComparison.OrdinalIgnoreCase, out durableTaskValue) && durableTaskValue != null)
            {
                var kvp = (JObject)durableTaskValue;

                JToken nameValue;
                if (kvp.TryGetValue(Constants.HubName, StringComparison.OrdinalIgnoreCase, out nameValue) && nameValue != null)
                {
                    config.Add(Constants.HubName, nameValue.ToString());
                }

                if (kvp.TryGetValue(Constants.DurableTaskStorageConnectionName, StringComparison.OrdinalIgnoreCase, out nameValue) && nameValue != null)
                {
                    config.Add(Constants.DurableTaskStorageConnection, nameValue.ToString());
                }
            }
        }

        private static IEnumerable<JObject> DeserializeFunctionTrigger(string functionJson)
        {
            try
            {
                var functionName = Path.GetFileName(Path.GetDirectoryName(functionJson));
                var json = JObject.Parse(File.ReadAllText(functionJson));

                JToken value;
                // https://github.com/Azure/azure-webjobs-sdk-script/blob/a9bafba78a3a8092bfd61a8c7093200dae867efb/src/WebJobs.Script/Host/ScriptHost.cs#L1476-L1498
                if (json.TryGetValue("disabled", out value))
                {
                    string stringValue = value.ToString();
                    bool disabled;
                    // if "disabled" is not a boolean, we try to expend it as an environment variable
                    if (!Boolean.TryParse(stringValue, out disabled))
                    {
                        string expandValue = System.Environment.GetEnvironmentVariable(stringValue);
                        // "1"/"true" -> true, else false
                        disabled = string.Equals(expandValue, "1", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(expandValue, "true", StringComparison.OrdinalIgnoreCase);
                    }

                    if (disabled)
                    {
                        Trace(TraceEventType.Verbose, "Function {0} is disabled", functionName);
                        return Enumerable.Empty<JObject>();
                    }
                }

                var excluded = json.TryGetValue("excluded", out value) && (bool)value;
                if (excluded)
                {
                    Trace(TraceEventType.Verbose, "Function {0} is excluded", functionName);
                    return Enumerable.Empty<JObject>();
                }

                var triggers = new List<JObject>();
                foreach (JObject binding in (JArray)json["bindings"])
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
                Trace(TraceEventType.Warning, "{0} is invalid. {1}", functionJson, ex);

                // Fail the deployment if invalid function.json
                throw;
            }
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

        private static async Task PostAsync(string path, string requestId, string content = null)
        {
            var host = HttpHost;
            var ipAddress = await GetAlternativeIPAddress(host);
            var statusCode = default(HttpStatusCode);
            try
            {
                using (var client = HttpClientFactory())
                {
                    if (ipAddress == null)
                    {
                        Trace(TraceEventType.Verbose, "Begin HttpPost https://{0}{1}, x-ms-request-id: {2}", host, path, requestId);
                        client.BaseAddress = new Uri(string.Format("https://{0}", host));
                    }
                    else
                    {
                        Trace(TraceEventType.Verbose, "Begin HttpPost https://{0}{1}, host: {2}, x-ms-request-id: {3}", ipAddress, path, host, requestId);
                        client.BaseAddress = new Uri(string.Format("https://{0}", ipAddress));
                        client.DefaultRequestHeaders.Host = host;
                    }

                    client.DefaultRequestHeaders.UserAgent.Add(_userAgent.Value);
                    client.DefaultRequestHeaders.Add(Constants.SiteRestrictedToken, SimpleWebTokenHelper.CreateToken(DateTime.UtcNow.AddMinutes(5)));
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

        /// <summary>
        /// This works around the hostname dns resolution issue for recently created site.
        /// If dns failed, we will use the home hosted service as alternative IP address.
        /// </summary>
        /// <returns></returns>
        private static async Task<IPAddress> GetAlternativeIPAddress(string host)
        {
            try
            {
                // if resolved successfully, return null to not use alternative ipAddress
                await Dns.GetHostEntryAsync(host);
                return null;
            }
            catch (Exception ex)
            {
                Trace(TraceEventType.Verbose, "Unable to dns resolve {0}.  {1}", host, ex);
            }

            return await GetHomeStampAddress(host);
        }

        private static async Task<IPAddress> GetHomeStampAddress(string host)
        {
            var homeStamp = HomeStamp;
            if (string.IsNullOrEmpty(homeStamp))
            {
                return null;
            }

            // cloudapp.net is the default to make it easy for private stamp testing.
            var homeStampHostName = string.Format("{0}.cloudapp.net", homeStamp);
            if (host.EndsWith(".scm.azurewebsites.us", StringComparison.OrdinalIgnoreCase))
            {
                homeStampHostName = string.Format("{0}.usgovcloudapp.net", homeStamp);
            }
            else if (host.EndsWith(".scm.chinacloudsites.cn", StringComparison.OrdinalIgnoreCase))
            {
                homeStampHostName = string.Format("{0}.chinacloudapp.cn", homeStamp);
            }
            else if (host.EndsWith(".scm.azurewebsites.de", StringComparison.OrdinalIgnoreCase))
            {
                homeStampHostName = string.Format("{0}.azurecloudapp.de", homeStamp);
            }

            try
            {
                Trace(TraceEventType.Verbose, "Try to dns resolve stamp {0}.", homeStampHostName);
                var entry = await Dns.GetHostEntryAsync(homeStampHostName);
                return entry.AddressList.First();
            }
            catch (Exception ex)
            {
                Trace(TraceEventType.Verbose, "Unable to dns resolve stamp {0}.  {1}", homeStampHostName, ex);
            }

            return null;
        }

        public static void RunPostDeploymentScripts(TraceListener tracer)
        {
            _tracer = tracer;

            foreach (var file in GetPostBuildActionScripts())
            {
                ExecuteScript(file);
            }
        }

        /// <summary>
        /// As long as the task was not completed, we will keep updating the marker file.
        /// The routine completes when either task completes or timeout.
        /// If task is completed, we will remove the marker.
        /// If timeout, we will leave the stale marker.
        /// </summary>
        public static async Task TrackPendingOperation(Task task, TimeSpan timeout)
        {
            const int DefaultTimeoutMinutes = 30;
            const int DefaultUpdateMarkerIntervalMS = 10000;
            const string MarkerFilePath = @"%TEMP%\SCMPendingOperation.txt";

            // only applicable to azure env
            if (!IsAzureEnvironment())
            {
                return;
            }

            if (timeout <= TimeSpan.Zero || timeout >= TimeSpan.FromMinutes(DefaultTimeoutMinutes))
            {
                // track at most N mins by default
                timeout = TimeSpan.FromMinutes(DefaultTimeoutMinutes);
            }

            var start = DateTime.UtcNow;
            var markerFile = System.Environment.ExpandEnvironmentVariables(MarkerFilePath);
            while (start.Add(timeout) >= DateTime.UtcNow)
            {
                // create or update marker timestamp
                OperationManager.SafeExecute(() => File.WriteAllText(markerFile, start.ToString("o")));

                var cancelation = new CancellationTokenSource();
                var delay = Task.Delay(DefaultUpdateMarkerIntervalMS, cancelation.Token);
                var completed = await Task.WhenAny(delay, task);
                if (completed != delay)
                {
                    cancelation.Cancel();
                    break;
                }
            }

            // remove marker
            OperationManager.SafeExecute(() => File.Delete(markerFile));
        }

        private static void ExecuteScript(string file)
        {
            var fi = new FileInfo(file);
            ProcessStartInfo processInfo;
            if (string.Equals(".ps1", fi.Extension, StringComparison.OrdinalIgnoreCase))
            {
                processInfo = new ProcessStartInfo("PowerShell.exe", string.Format("-ExecutionPolicy RemoteSigned -File \"{0}\"", file));
            }
            else
            {
                processInfo = new ProcessStartInfo(file);
            }

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardInput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            DataReceivedEventHandler stdoutHandler = (object sender, DataReceivedEventArgs e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Trace(TraceEventType.Information, "{0}", e.Data);
                }
            };

            DataReceivedEventHandler stderrHandler = (object sender, DataReceivedEventArgs e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Trace(TraceEventType.Error, "{0}", e.Data);
                }
            };

            Trace(TraceEventType.Information, "Run post-deployment: \"{0}\" {1}", processInfo.FileName, processInfo.Arguments);
            var process = Process.Start(processInfo);
            var processName = process.ProcessName;
            var processId = process.Id;
            Trace(TraceEventType.Information, "Process {0}({1}) started", processName, processId);

            // hook stdout and stderr
            process.OutputDataReceived += stdoutHandler;
            process.BeginOutputReadLine();
            process.ErrorDataReceived += stderrHandler;
            process.BeginErrorReadLine();

            var timeout = (int)GetCommandTimeOut().TotalMilliseconds;
            if (!process.WaitForExit(timeout))
            {
                process.Kill();
                throw new TimeoutException(String.Format("Process {0}({1}) exceeded {2}ms timeout", processName, processId, timeout));
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(String.Format("Process {0}({1}) exited with {2} exitcode.", processName, processId, process.ExitCode));
            }

            Trace(TraceEventType.Information, "Process {0}({1}) executed successfully.", processName, processId);
        }

        private static TimeSpan GetCommandTimeOut()
        {
            const int DefaultCommandTimeout = 60;

            var val = System.Environment.GetEnvironmentVariable(SettingsKeys.CommandIdleTimeout);
            if (!string.IsNullOrEmpty(val))
            {
                int commandTimeout;
                if (Int32.TryParse(val, out commandTimeout) && commandTimeout > 0)
                {
                    return TimeSpan.FromSeconds(commandTimeout);
                }
            }

            return TimeSpan.FromSeconds(DefaultCommandTimeout);
        }

        private static IEnumerable<string> GetPostBuildActionScripts()
        {
            // "/site/deployments/tools/PostDeploymentActions" (can override with %SCM_POST_DEPLOYMENT_ACTIONS_PATH%)
            // if %SCM_POST_DEPLOYMENT_ACTIONS_PATH% is set, it is absolute path to the post-deployment script folder
            var postDeploymentPath = System.Environment.GetEnvironmentVariable(SettingsKeys.PostDeploymentActionsDirectory);
            if (string.IsNullOrEmpty(postDeploymentPath))
            {
                postDeploymentPath = System.Environment.ExpandEnvironmentVariables(@"%HOME%\site\deployments\tools\PostDeploymentActions");
            }

            if (!Directory.Exists(postDeploymentPath))
            {
                return Enumerable.Empty<string>();
            }

            // Find all post action scripts and order file alphabetically for each folder
            return Directory.GetFiles(postDeploymentPath, "*", SearchOption.TopDirectoryOnly)
                                    .Where(f => f.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                                        || f.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                                        || f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(n => n);
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

        public static async Task UpdateSiteVersion(ZipDeploymentInfo deploymentInfo, IEnvironment environment, ILogger logger)
        {
            var siteVersionPath = Path.Combine(environment.SitePackagesPath, Constants.SiteVersionTxt);
            logger.Log($"Updating {siteVersionPath} with deployment {deploymentInfo.ZipName}");
            await FileSystemHelpers.WriteAllTextToFileAsync(siteVersionPath, deploymentInfo.ZipName);
        }
    }
}
