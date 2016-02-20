using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using NuGet;
using System.Net.Http;

namespace Kudu.Core.Functions
{
    public class FunctionManager : IFunctionManager
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;

        public FunctionManager(IEnvironment environment, ITraceFactory traceFactory)
        {
            _environment = environment;
            _traceFactory = traceFactory;
        }

        public async Task SyncTriggers()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionManager.SyncTriggers"))
            {
                if (!IsFunctionEnabled())
                {
                    tracer.Trace("This is not a function-enabled site!");
                    return; 
                }

                var inputs = await GetTriggerInputs(tracer);
                if (inputs.Count == 0)
                {
                    tracer.Trace("No input triggers!");
                    return;
                }

                var client = new OperationClient(tracer);
                await client.PostAsync("/operations/settriggers", inputs);
            }
        }

        public bool IsFunctionEnabled()
        {
            // this should read appSettings instead
            var hostJson = Path.Combine(_environment.WebRootPath, Constants.FunctionsHostConfigFile);
            return FileSystemHelpers.FileExists(hostJson);
        }

        public async Task<JArray> GetTriggerInputs(ITracer tracer)
        {
            JArray inputs = new JArray();
            foreach (var functionJson in await ListFunctionsConfig())
            {
                try
                {
                    var binding = functionJson.Config.Value<JObject>("bindings");
                    foreach (JObject input in binding.Value<JArray>("input"))
                    {
                        var type = input.Value<string>("type");
                        if (type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))
                        {
                            tracer.Trace(String.Format("Sync {0} of {1}", type, functionJson.Name));
                            inputs.Add(input);
                        }
                        else
                        {
                            tracer.Trace(String.Format("Skip {0} of {1}", type, functionJson.Name));
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracer.Trace(String.Format("{0} is invalid. {1}", functionJson.Name, ex.Message));
                }
            }

            return inputs;
        }

        public async Task<FunctionEnvelope> CreateOrUpdate(string name, FunctionEnvelope functionEnvelope)
        {
            var functionDir = Path.Combine(_environment.FunctionsPath, name);

            // Assert templateId and value are both present
            if (!string.IsNullOrEmpty(functionEnvelope?.TemplateId))
            {
                // check to create template.
                if (FileSystemHelpers.DirectoryExists(functionDir) &&
                    FileSystemHelpers.GetFileSystemEntries(functionDir).Any())
                {
                    throw new InvalidOperationException($"Function {name} already exist");
                }

                var template = (await GetTemplatesFromGithub()).FirstOrDefault(e => e.name.Equals(functionEnvelope.TemplateId, StringComparison.OrdinalIgnoreCase));

                if (template != null)
                {
                    await DeployTemplateFromGithub(template, functionDir);
                }
                else
                {
                    throw new FileNotFoundException($"template: {functionEnvelope.TemplateId} was not found");
                }
            }
            else
            {
                // Make sure the function folder exists
                FileSystemHelpers.EnsureDirectory(functionDir);

                // If files are included, write them out
                if (functionEnvelope?.Files != null)
                {
                    // Delete all existing files in the directory. This will also delete current function.json, but it gets recreated below
                    FileSystemHelpers.DeleteDirectoryContentsSafe(functionDir);

                    foreach (JProperty prop in functionEnvelope?.Files.Properties())
                    {
                        await FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, prop.Name), (string)prop.Value);
                    }
                }

                // Create the function.json
                await FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, Constants.FunctionsConfigFile), JsonConvert.SerializeObject(functionEnvelope?.Config ?? new JObject()));
            }
            return await GetFunctionConfig(name);
        }

        public async Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfig()
        {
            var configList = await Task.WhenAll(
                    FileSystemHelpers
                    .GetDirectories(_environment.FunctionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(FileSystemHelpers.FileExists)
                    .Select(async f => { try { return await GetFunctionConfig(Path.GetFileName(Path.GetDirectoryName(f))); } catch { return null; } }));
            return configList.Where(c => c != null);
        }

        public async Task<FunctionEnvelope> GetFunctionConfig(string name)
        {
            var path = Path.Combine(GetFunctionPath(name), Constants.FunctionsConfigFile);
            if (FileSystemHelpers.FileExists(path))
            {
                return CreateFunctionConfig(await FileSystemHelpers.ReadAllTextFromFileAsync(path), name);
            }

            throw new FileNotFoundException($"Function ({path}) does not exist");
        }

        public async Task<JObject> GetHostConfig()
        {
            var path = Path.Combine(_environment.FunctionsPath, Constants.FunctionsHostConfigFile);
            if (FileSystemHelpers.FileExists(path))
            {
                return JObject.Parse(await FileSystemHelpers.ReadAllTextFromFileAsync(path));
            }

            throw new FileNotFoundException($"Host file ({path}) does not exist");
        }

        public async Task<JObject> PutHostConfig(JObject content)
        {
            var path = Path.Combine(_environment.FunctionsPath, Constants.FunctionsHostConfigFile);
            await FileSystemHelpers.WriteAllTextToFileAsync(path, JsonConvert.SerializeObject(content));
            return await GetHostConfig();
        }

        public FunctionEnvelope CreateFunctionConfig(string configContent, string functionName)
        {
            var config = JObject.Parse(configContent);
            return new FunctionEnvelope
            {
                Name = functionName,
                ScriptRootPathHref = FilePathToVfsUri(GetFunctionPath(functionName), isDirectory: true),
                ScriptHref = FilePathToVfsUri(GetFunctionScriptPath(functionName, config)),
                ConfigHref = FilePathToVfsUri(Path.Combine(GetFunctionPath(functionName), Constants.FunctionsConfigFile)),
                TestDataHref = FilePathToVfsUri(GetFunctionSampleDataFile(functionName)),
                SecretsFileHref = FilePathToVfsUri(GetFunctionSecretsFile(functionName)),
                Href = GetFunctionHref(functionName),
                Config = config
            };
        }

        public IEnumerable<FunctionTemplate> GetTemplates()
        {
            return new[]
            {
                new FunctionTemplate { Id = "BlobTrigger-CSharp", Language = "C#", Trigger = "Blob" },
                new FunctionTemplate { Id = "BlobTrigger", Language = "JavaScript", Trigger = "Blob" },
                new FunctionTemplate { Id = "HttpTrigger-Batch", Language = "Batch", Trigger = "Http" },
                new FunctionTemplate { Id = "HttpTrigger-CSharp", Language = "C#", Trigger = "Http" },
                new FunctionTemplate { Id = "HttpTrigger", Language = "JavaScript", Trigger = "Http" },
                new FunctionTemplate { Id = "ManualTrigger", Language = "JavaScript", Trigger = "Manual" },
                new FunctionTemplate { Id = "QueueTrigger-Bash", Language = "Bash", Trigger = "Queue" },
                new FunctionTemplate { Id = "QueueTrigger-Batch", Language = "Batch", Trigger = "Queue" },
                new FunctionTemplate { Id = "QueueTrigger-FSharp", Language = "F#", Trigger = "Queue" },
                new FunctionTemplate { Id = "QueueTrigger-Php", Language = "Php", Trigger = "Queue" },
                new FunctionTemplate { Id = "QueueTrigger-Powershell", Language = "PowerShell", Trigger = "Queue" },
                new FunctionTemplate { Id = "QueueTrigger-Python", Language = "Python", Trigger = "Queue" },
                new FunctionTemplate { Id = "QueueTrigger", Language = "JavaScript", Trigger = "Queue" },
                new FunctionTemplate { Id = "ResizeImage", Language = "exe", Trigger = "Queue" },
                new FunctionTemplate { Id = "ServiceBusQueueTrigger", Language = "JavaScript", Trigger = "ServiceBus" },
                new FunctionTemplate { Id = "TimerTrigger", Language = "JavaScript", Trigger = "Timer" },
                new FunctionTemplate { Id = "TimerTrigger-CSharp", Language = "C#", Trigger = "Timer" },
                new FunctionTemplate { Id = "WebHook-Generic", Language = "JavaScript", Trigger = "WebHook-Generic" },
                new FunctionTemplate { Id = "WebHook-GitHub", Language = "JavaScript", Trigger = "WebHook-GitHub" }
            };
        }

        public void DeleteFunction(string name)
        {
            FileSystemHelpers.DeleteDirectorySafe(GetFunctionPath(name), ignoreErrors: false);
            FileSystemHelpers.DeleteFileSafe(GetFunctionSampleDataFile(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionSecretsFile(name));
        }

        private string GetFunctionSampleDataFile(string functionName)
        {
            return Path.Combine(_environment.DataPath, Constants.Functions, Constants.SampleData, $"{functionName}.dat");
        }

        private string GetFunctionSecretsFile(string functionName)
        {
            return Path.Combine(_environment.DataPath, Constants.Functions, Constants.Secrets, $"{functionName}.json");
        }

        // Logic for this function is copied from here
        // https://github.com/Azure/azure-webjobs-sdk-script/blob/e0a783e882dd8680bf23e3c8818fb9638071c21d/src/WebJobs.Script/Config/ScriptHost.cs#L113-L150
        private string GetFunctionScriptPath(string functionName, JObject functionInfo)
        {
            var functionPath = GetFunctionPath(functionName);
            var functionFiles = FileSystemHelpers.GetFiles(functionPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(p => Path.GetFileName(p).ToLowerInvariant() != "function.json").ToArray();

            if (functionFiles.Length == 0)
            {
                return functionPath;
            }
            else if (functionFiles.Length == 1)
            {
                // if there is only a single file, that file is primary
                return functionFiles[0];
            }
            else
            {
                // if there is a "run" file, that file is primary
                string functionPrimary = null;
                functionPrimary = functionFiles.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == "run");
                if (string.IsNullOrEmpty(functionPrimary))
                {
                    // for Node, any index.js file is primary
                    functionPrimary = functionFiles.FirstOrDefault(p => Path.GetFileName(p).ToLowerInvariant() == "index.js");
                    if (string.IsNullOrEmpty(functionPrimary))
                    {
                        // finally, if there is an explicit primary file indicated
                        // in config, use it
                        JToken token = functionInfo["source"];
                        if (token != null)
                        {
                            string sourceFileName = (string)token;
                            functionPrimary = Path.Combine(functionPath, sourceFileName);
                        }
                    }
                }

                if (string.IsNullOrEmpty(functionPrimary))
                {
                    // TODO: should this be an error?
                    return functionPath;
                }
                return functionPrimary;
            }
        }

        private Uri FilePathToVfsUri(string filePath, bool isDirectory = false)
        {
            filePath = filePath.Substring(_environment.RootPath.Length).Trim('\\').Replace("\\", "/");
            return new Uri($"{_environment.AppBaseUrlPrefix}/api/vfs/{filePath}{(isDirectory ? "/" : string.Empty)}");
        }

        private Uri GetFunctionHref(string functionName)
        {
            return new Uri($"{_environment.AppBaseUrlPrefix}/api/functions/{functionName}");
        }

        private string GetFunctionPath(string name)
        {
            var path = Path.Combine(_environment.FunctionsPath, name);
            if (FileSystemHelpers.DirectoryExists(path))
            {
                return path;
            }

            throw new FileNotFoundException($"Function ({path}) does not exist");
        }

        private static async Task<IEnumerable<GitHubContent>> GetTemplatesFromGithub()
        {
            using (var client = GetHttpClient())
            {
                var response = await client.GetAsync("https://api.github.com/repos/fabiocav/azure-webjobs-sdk-script/contents/sample?ref=3b5a5bcefc57f432a67ce1c30386ce7620cca1ef");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsAsync<IEnumerable<GitHubContent>>();
                return content.Where(s => s.type.Equals("dir", StringComparison.OrdinalIgnoreCase));
            }
        }

        private static HttpClient GetHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Kudu/Api");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            return client;
        }

        private async static Task DeployTemplateFromGithub(GitHubContent template, string functionDir)
        {
            //deploy template from github
            FileSystemHelpers.EnsureDirectory(functionDir);
            using (var webClient = new WebClient())
            using (var httpClient = GetHttpClient())
            {
                var response = await httpClient.GetAsync(template.url);
                response.EnsureSuccessStatusCode();
                var files = await response.Content.ReadAsAsync<IEnumerable<GitHubContent>>();
                foreach (var file in files.Where(s => s.type.Equals("file", StringComparison.OrdinalIgnoreCase)))
                {
                    await webClient.DownloadFileTaskAsync(new Uri(file.download_url), Path.Combine(functionDir, file.name));
                }
            }
        }
    }

    public class GitHubContent
    {
        public string name { get; set; }
        public string path { get; set; }
        public string sha { get; set; }
        public int size { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string git_url { get; set; }
        public string download_url { get; set; }
        public string type { get; set; }
    }
}