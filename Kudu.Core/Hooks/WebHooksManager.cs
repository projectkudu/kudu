using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Kudu.Core.Hooks
{
    public class WebHooksManager : IWebHooksManager
    {
        private const string HooksFileName = "hooks";

        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly IEnvironment _environment;
        private readonly string _hooksFilePath;
        private readonly IOperationLock _hooksLock;
        private readonly ITracer _tracer;

        static WebHooksManager()
        {
            JsonSerializerSettings.Converters.Add(new StringEnumConverter() { CamelCaseText = true });
        }

        public WebHooksManager(ITracer tracer, IEnvironment environment, IOperationLock hooksLock)
        {
            _tracer = tracer;
            _environment = environment;
            _hooksLock = hooksLock;

            _hooksFilePath = Path.Combine(_environment.DeploymentsPath, HooksFileName);
        }

        public IEnumerable<WebHook> WebHooks
        {
            get
            {
                using (_tracer.Step("WebHooksManager.WebHooks"))
                {
                    IEnumerable<WebHook> webHooks = null;

                    _hooksLock.LockOperation(() =>
                    {
                        webHooks = ReadWebHooksFromFile();
                    }, "Getting WebHooks", LockTimeout);

                    return webHooks;
                }
            }
        }

        public WebHook AddWebHook(WebHook webHook)
        {
            using (_tracer.Step("WebHooksManager.AddWebHook"))
            {
                // must be valid absolute uri.
                if (!Uri.IsWellFormedUriString(webHook.HookAddress, UriKind.Absolute))
                {
                    throw new FormatException(Resources.Error_InvalidHookAddress.FormatCurrentCulture(webHook.HookAddress));
                }

                WebHook createdWebHook = null;

                _hooksLock.LockOperation(() =>
                {
                    var webHooks = new List<WebHook>(ReadWebHooksFromFile());
                    WebHook existingWebHook = webHooks.FirstOrDefault(h => String.Equals(h.HookAddress, webHook.HookAddress, StringComparison.OrdinalIgnoreCase));

                    if (existingWebHook == null)
                    {
                        // if web hook doesn't exist (by address) then add it
                        createdWebHook = new WebHook(webHook.HookEventType, webHook.HookAddress, id: DateTime.UtcNow.Ticks.ToString(), insecureSsl: webHook.InsecureSsl);
                        webHooks.Add(createdWebHook);
                        SaveHooksToFile(webHooks);

                        _tracer.Trace("Added web hook: type - {0}, address - {1}", createdWebHook.HookEventType, createdWebHook.HookAddress);
                    }
                    else if (String.Equals(webHook.HookEventType, existingWebHook.HookEventType, StringComparison.OrdinalIgnoreCase))
                    {
                        // if web hook exist with the same hook event type, return the existing one
                        createdWebHook = existingWebHook;
                    }
                    else
                    {
                        // if web hook exists but with a different hook event type then throw a conflict exception
                        throw new ConflictException();
                    }
                }, "Adding WebHook", LockTimeout);

                return createdWebHook;
            }
        }

        public void RemoveWebHook(string hookId)
        {
            using (_tracer.Step("WebHooksManager.RemoveWebHook"))
            {
                _hooksLock.LockOperation(() =>
                {
                    RemoveWebHookNotUnderLock(hookId);
                }, "Deleting WebHook", LockTimeout);
            }
        }

        public WebHook GetWebHook(string hookId)
        {
            using (_tracer.Step("WebHooksManager.GetWebHook"))
            {
                return WebHooks.FirstOrDefault(h => String.Equals(h.Id, hookId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public async Task PublishEventAsync(string hookEventType, object eventContent)
        {
            using (_tracer.Step("WebHooksManager.PublishEventAsync: " + hookEventType))
            {
                string jsonString = JsonConvert.SerializeObject(eventContent, JsonSerializerSettings);

                await _hooksLock.LockOperationAsync(async () =>
                {
                    await PublishToHooksAsync(jsonString, hookEventType);
                }, "Publishing WebHook event", LockTimeout);
            }
        }

        private void RemoveWebHookNotUnderLock(string hookId)
        {
            IEnumerable<WebHook> hooks = ReadWebHooksFromFile();
            SaveHooksToFile(hooks.Where(h => !String.Equals(h.Id, hookId, StringComparison.OrdinalIgnoreCase)));
        }

        private async Task PublishToHookAsync(WebHook webHook, string jsonString)
        {
            using (HttpClient httpClient = CreateHttpClient(webHook))
            {
                using (var content = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json"))
                {
                    try
                    {
                        _tracer.Trace("Publish {0}#{1} to address - {2}, json - {3}, insecure - {4}", webHook.HookEventType, webHook.Id, webHook.HookAddress, jsonString, webHook.InsecureSsl);

                        webHook.LastPublishDate = DateTime.UtcNow;
                        webHook.LastContext = jsonString;

                        using (HttpResponseMessage response = await httpClient.PostAsync(webHook.HookAddress, content))
                        {
                            _tracer.Trace("Publish {0}#{1} to address - {2}, response - {3}", webHook.HookEventType, webHook.Id, webHook.HookAddress, response.StatusCode);

                            // Handle 410 responses by removing the web hook
                            if (response.StatusCode == HttpStatusCode.Gone)
                            {
                                RemoveWebHookNotUnderLock(webHook.Id);
                            }

                            webHook.LastPublishStatus = response.StatusCode.ToString();
                            webHook.LastPublishReason = response.ReasonPhrase;
                        }
                    }
                    catch (Exception ex)
                    {
                        _tracer.Trace("Error while publishing hook - {0}#{1}, to address - {1}", webHook.HookEventType, webHook.Id, webHook.HookAddress);
                        _tracer.TraceError(ex);

                        webHook.LastPublishStatus = "Failure";
                        webHook.LastPublishReason = ex.Message;
                    }
                }
            }
        }

        private static HttpClient CreateHttpClient(WebHook webHook)
        {
            var webRequestHandler = new WebRequestHandler();

            var hookAddress = new Uri(webHook.HookAddress);
            string userInfo = hookAddress.UserInfo;
            if (userInfo != null)
            {
                string[] userInfos = userInfo.Split(':');
                if (userInfos.Length == 2)
                {
                    webRequestHandler.Credentials = new NetworkCredential(userInfos[0], userInfos[1]);
                }
            }

            if (webHook.InsecureSsl)
            {
                webRequestHandler.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }

            return new HttpClient(webRequestHandler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        private async Task PublishToHooksAsync(string jsonString, string hookType)
        {
            IEnumerable<WebHook> webHooks = ReadWebHooksFromFile();

            if (webHooks.Any())
            {
                var publishTasks = new List<Task>();

                foreach (var webHook in webHooks.Where(h => String.Equals(h.HookEventType, hookType, StringComparison.OrdinalIgnoreCase)))
                {
                    // this is to address the bug where we used to relax and allow relative path
                    if (Uri.IsWellFormedUriString(webHook.HookAddress, UriKind.Absolute))
                    {
                        Task publishTask = PublishToHookAsync(webHook, jsonString);
                        publishTasks.Add(publishTask);
                    }
                }

                await Task.WhenAll(publishTasks);

                SaveHooksToFile(webHooks);
            }
        }

        private IEnumerable<WebHook> ReadWebHooksFromFile()
        {
            string fileContent = null;

            if (!FileSystemHelpers.FileExists(_hooksFilePath))
            {
                return Enumerable.Empty<WebHook>();
            }

            OperationManager.Attempt(() => fileContent = FileSystemHelpers.ReadAllText(_hooksFilePath));

            if (!String.IsNullOrEmpty(fileContent))
            {
                try
                {
                    // It is possible for Deserialize to not throw and return null.
                    return JsonConvert.DeserializeObject<IEnumerable<WebHook>>(fileContent, JsonSerializerSettings) ?? Enumerable.Empty<WebHook>();
                }
                catch (JsonSerializationException ex)
                {
                    _tracer.TraceError(ex);
                }
            }

            return Enumerable.Empty<WebHook>();
        }

        private void SaveHooksToFile(IEnumerable<WebHook> hooks)
        {
            string hooksFileContent = JsonConvert.SerializeObject(hooks, JsonSerializerSettings);
            OperationManager.Attempt(() => FileSystemHelpers.WriteAllText(_hooksFilePath, hooksFileContent));
        }
    }
}