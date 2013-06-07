using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
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
        private readonly IFileSystem _fileSystem;
        private readonly ITracer _tracer;

        static WebHooksManager()
        {
            JsonSerializerSettings.Converters.Add(new StringEnumConverter() { CamelCaseText = true });
        }

        public WebHooksManager(ITracer tracer, IEnvironment environment, IOperationLock hooksLock, IFileSystem fileSystem)
        {
            _tracer = tracer;
            _environment = environment;
            _hooksLock = hooksLock;
            _fileSystem = fileSystem;

            _hooksFilePath = Path.Combine(_environment.DeploymentsPath, HooksFileName);
        }

        public IEnumerable<WebHook> WebHooks
        {
            get
            {
                IEnumerable<WebHook> webHooks = null;

                _hooksLock.LockOperation(() =>
                {
                    webHooks = ReadWebHooksFromFile();
                }, LockTimeout);

                return webHooks;
            }
        }

        public WebHook AddWebHook(WebHook webHook)
        {
            using (_tracer.Step("WebHooksManager.AddWebHook"))
            {
                if (!Uri.IsWellFormedUriString(webHook.HookAddress, UriKind.RelativeOrAbsolute))
                {
                    throw new FormatException(Resources.Error_InvalidHookAddress.FormatCurrentCulture(webHook.HookAddress));
                }

                WebHook createdWebHook = null;

                _hooksLock.LockOperation(() =>
                {
                    createdWebHook = new WebHook(webHook.HookEventType, webHook.HookAddress, id: DateTime.UtcNow.Ticks.ToString(), insecureSsl: webHook.InsecureSsl);

                    var webHooks = new List<WebHook>(ReadWebHooksFromFile());
                    if (!webHooks.Any(h => String.Equals(h.HookAddress, createdWebHook.HookAddress, StringComparison.OrdinalIgnoreCase)))
                    {
                        webHooks.Add(createdWebHook);
                        SaveHooksToFile(webHooks);

                        _tracer.Trace("Added web hook: type - {0}, address - {1}", createdWebHook.HookEventType, createdWebHook.HookAddress);
                    }
                    else
                    {
                        throw new ConflictException();
                    }
                }, LockTimeout);

                return createdWebHook;
            }
        }

        public void RemoveWebHook(string hookId)
        {
            _hooksLock.LockOperation(() =>
            {
                IEnumerable<WebHook> hooks = ReadWebHooksFromFile();
                SaveHooksToFile(hooks.Where(h => !String.Equals(h.Id, hookId, StringComparison.OrdinalIgnoreCase)));
            }, LockTimeout);
        }

        public WebHook GetWebHook(string hookId)
        {
            return WebHooks.FirstOrDefault(h => String.Equals(h.Id, hookId, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<WebHook> GetWebHooks(string hookEventType)
        {
            return WebHooks.Where(h => String.Equals(h.HookEventType, hookEventType, StringComparison.OrdinalIgnoreCase));
        }

        public async Task PublishPostDeploymentAsync(IDeploymentStatusFile statusFile)
        {
            string jsonString = JsonConvert.SerializeObject(statusFile, JsonSerializerSettings);

            await PublishToHooksAsync(jsonString, HookEventTypes.PostDeployment);
        }

        private async Task PublishToHookAsync(WebHook webHook, string jsonString)
        {
            try
            {
                WebRequestHandler webRequestHandler = null;
                HttpClient httpClient = null;

                if (webHook.InsecureSsl)
                {
                    webRequestHandler = new WebRequestHandler()
                    {
                        ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                    };
                    httpClient = new HttpClient(webRequestHandler);
                }
                else
                {
                    httpClient = new HttpClient();
                }
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                using (httpClient)
                {
                    using (var content = new StringContent(jsonString))
                    {
                        _tracer.Trace("Publish {0}#{1} to address - {2}, json - {3}, insecure - {4}", webHook.HookEventType, webHook.Id, webHook.HookAddress, jsonString, webHook.InsecureSsl);

                        using (HttpResponseMessage response = await httpClient.PostAsync(webHook.HookAddress, content))
                        {
                            _tracer.Trace("Publish {0}#{1} to address - {2}, response - {3}", webHook.HookEventType, webHook.Id, webHook.HookAddress, response.StatusCode);

                            // Handle 410 responses by removing the web hook
                            if (response.StatusCode == HttpStatusCode.Gone)
                            {
                                RemoveWebHook(webHook.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace("Error while publishing for hook type - {0}, to address - {1}", webHook.HookEventType, webHook.HookAddress);
                _tracer.TraceError(ex);
            }
        }

        private async Task PublishToHooksAsync(string jsonString, string hookType)
        {
            IEnumerable<WebHook> webHooks = GetWebHooks(hookType);

            if (webHooks.Any())
            {
                var publishTasks = new List<Task>();

                foreach (var webHook in webHooks)
                {
                    Task publishTask = PublishToHookAsync(webHook, jsonString);
                    publishTasks.Add(publishTask);
                }

                await Task.WhenAll(publishTasks);
            }
        }

        private IEnumerable<WebHook> ReadWebHooksFromFile()
        {
            string fileContent = null;

            if (!_fileSystem.File.Exists(_hooksFilePath))
            {
                return Enumerable.Empty<WebHook>();
            }

            OperationManager.Attempt(() => fileContent = _fileSystem.File.ReadAllText(_hooksFilePath));

            if (!String.IsNullOrEmpty(fileContent))
            {
                try
                {
                    return JsonConvert.DeserializeObject<IEnumerable<WebHook>>(fileContent, JsonSerializerSettings);
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
            OperationManager.Attempt(() => _fileSystem.File.WriteAllText(_hooksFilePath, hooksFileContent));
        }
    }
}