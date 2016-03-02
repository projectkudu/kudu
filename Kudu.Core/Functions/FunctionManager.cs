using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;
using System.Linq;
using Newtonsoft.Json;

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

        public async Task SyncTriggersAsync()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionManager.SyncTriggers"))
            {
                if (!IsFunctionEnabled)
                {
                    tracer.Trace("This is not a function-enabled site!");
                    return; 
                }

                var inputs = await GetTriggerInputsAsync(tracer);
                if (inputs.Count == 0)
                {
                    tracer.Trace("No input triggers!");
                    return;
                }

                if (Environment.IsAzureEnvironment())
                {
                    var client = new OperationClient(tracer);
                    await client.PostAsync("/operations/settriggers", inputs);
                }
            }
        }

        private bool IsFunctionEnabled
        {
            // this should read appSettings instead
            get { return FileSystemHelpers.FileExists(HostJsonPath); }
        }

        private async Task<JArray> GetTriggerInputsAsync(ITracer tracer)
        {
            JArray inputs = new JArray();
            foreach (var functionJson in await ListFunctionsConfigAsync())
            {
                try
                {
                    JToken disabled;
                    if (functionJson.Config.TryGetValue("disabled", out disabled) && (bool)disabled)
                    {
                        tracer.Trace(String.Format("{0} is disabled", functionJson));
                        continue;
                    }

                    foreach (JObject input in functionJson.Config.Value<JArray>("bindings"))
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

        public async Task<FunctionEnvelope> CreateOrUpdateAsync(string name, FunctionEnvelope functionEnvelope)
        {
            var functionDir = Path.Combine(_environment.FunctionsPath, name);

            // Make sure the function folder exists
            FileSystemHelpers.EnsureDirectory(functionDir);

            // If files are included, write them out
            if (functionEnvelope?.Files != null)
            {
                // Delete all existing files in the directory. This will also delete current function.json, but it gets recreated below
                FileSystemHelpers.DeleteDirectoryContentsSafe(functionDir);

                await Task.WhenAll(functionEnvelope.Files.Select(e => FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, e.Key), e.Value)));
            }

            // Write out the config file if it's in the manifest
            if (functionEnvelope?.Config != null)
            {
                await FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, Constants.FunctionsConfigFile), JsonConvert.SerializeObject(functionEnvelope?.Config, Formatting.Indented));
            }

            return await GetFunctionConfigAsync(name);
        }

        public async Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfigAsync()
        {
            var configList = await Task.WhenAll(
                    FileSystemHelpers
                    .GetDirectories(_environment.FunctionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(FileSystemHelpers.FileExists)
                    .Select(f => TryGetFunctionConfigAsync(Path.GetFileName(Path.GetDirectoryName(f)))));
            return configList.Where(c => c != null);
        }

        public async Task<FunctionEnvelope> GetFunctionConfigAsync(string name)
        {
            var config = await TryGetFunctionConfigAsync(name);
            if (config == null)
            {
                throw new FileNotFoundException($"Function ({name}) config does not exist or is invalid");
            }
            return config;
        }

        public async Task<JObject> GetHostConfigAsync()
        {
            return FileSystemHelpers.FileExists(HostJsonPath)
                ? JObject.Parse(await FileSystemHelpers.ReadAllTextFromFileAsync(HostJsonPath))
                : new JObject();
        }

        public async Task<JObject> PutHostConfigAsync(JObject content)
        {
            await FileSystemHelpers.WriteAllTextToFileAsync(HostJsonPath, JsonConvert.SerializeObject(content));
            return await GetHostConfigAsync();
        }

        public void DeleteFunction(string name)
        {
            FileSystemHelpers.DeleteDirectorySafe(GetFunctionPath(name), ignoreErrors: false);
            FileSystemHelpers.DeleteFileSafe(GetFunctionSampleDataFilePath(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionSecretsFilePath(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionLogPath(name));
        }

        private async Task<FunctionEnvelope> TryGetFunctionConfigAsync(string name)
        {
            try
            {
                var path = GetFunctionConfigPath(name);
                if (FileSystemHelpers.FileExists(path))
                {
                    return CreateFunctionConfig(await FileSystemHelpers.ReadAllTextFromFileAsync(path), name);
                }
            }
            catch
            {
                // no-op
            }
            return null;
        }

        private FunctionEnvelope CreateFunctionConfig(string configContent, string functionName)
        {
            var config = JObject.Parse(configContent);
            return new FunctionEnvelope
            {
                Name = functionName,
                TriggerUrl = GetFunctionTriggerUrl(functionName),
                ScriptRootPathHref = FilePathToVfsUri(GetFunctionPath(functionName), isDirectory: true),
                ScriptHref = FilePathToVfsUri(GetFunctionScriptPath(functionName, config)),
                ConfigHref = FilePathToVfsUri(GetFunctionConfigPath(functionName)),
                TestDataHref = FilePathToVfsUri(GetFunctionSampleDataFilePath(functionName)),
                SecretsFileHref = FilePathToVfsUri(GetFunctionSecretsFilePath(functionName)),
                Href = GetFunctionHref(functionName),
                Config = config
            };
        }

        private string GetFunctionTriggerUrl(string functionName)
        {
            FunctionSecrets functionSecrets = GetFunctionSecrets(functionName);

            // TODO: 'code' is only valid for WebHooks. Need to generalize
            return String.Format(@"https://{0}/api/{1}?code={2}",
                System.Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost",
                functionName,
                functionSecrets.Key);
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

        private string HostJsonPath
        {
            get
            {
                return Path.Combine(_environment.FunctionsPath, Constants.FunctionsHostConfigFile);
            }
        }

        private string GetFunctionPath(string name)
        {
            var path = Path.Combine(_environment.FunctionsPath, name);
            if (FileSystemHelpers.DirectoryExists(path))
            {
                return path;
            }

            throw new FileNotFoundException($"Function ({name}) does not exist");
        }

        private string GetFunctionConfigPath(string name)
        {
            return Path.Combine(GetFunctionPath(name), Constants.FunctionsConfigFile);
        }

        private string GetFunctionLogPath(string name)
        {
            return Path.Combine(_environment.ApplicationLogFilesPath, Constants.Functions, Constants.Function, name);
        }

        private string GetFunctionSampleDataFilePath(string functionName)
        {
            return Path.Combine(_environment.DataPath, Constants.Functions, Constants.SampleData, $"{functionName}.dat");
        }

        private FunctionSecrets GetFunctionSecrets(string functionName)
        {
            FunctionSecrets secrets;
            string secretFilePath = GetFunctionSecretsFilePath(functionName);
            if (File.Exists(secretFilePath))
            {
                // load the secrets file
                string secretsJson = FileSystemHelpers.ReadAllTextFromFile(secretFilePath);
                secrets = JsonConvert.DeserializeObject<FunctionSecrets>(secretsJson);
            }
            else
            {
                // initialize with new secrets and save it
                secrets = new FunctionSecrets
                {
                    Key = SecurityUtility.GenerateSecretString()
                };

                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(secretFilePath));
                FileSystemHelpers.WriteAllText(secretFilePath, JsonConvert.SerializeObject(secrets, Formatting.Indented));
            }

            return secrets;
        }

        private string GetFunctionSecretsFilePath(string functionName)
        {
            return Path.Combine(_environment.DataPath, Constants.Functions, Constants.Secrets, $"{functionName}.json");
        }

        class FunctionSecrets
        {
            [JsonProperty(PropertyName = "key")]
            public string Key { get; set; }
        }
    }
}