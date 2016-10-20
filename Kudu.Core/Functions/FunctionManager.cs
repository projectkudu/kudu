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

        public async Task SyncTriggersAsync(ITracer tracer = null)
        {
            tracer = tracer ?? _traceFactory.GetTracer();
            using (tracer.Step("FunctionManager.SyncTriggers"))
            {
                if (!IsFunctionsSiteExtensionEnabled)
                {
                    tracer.Trace("Functions are not enabled for this site.");
                    return; 
                }

                var jwt = System.Environment.GetEnvironmentVariable(Constants.SiteRestrictedJWT);
                if (String.IsNullOrEmpty(jwt))
                {
                    // If there is no token, do nothing. This can happen on non-dynamic stamps
                    tracer.Trace("Ignoring operation as we don't have a token");
                    return;
                }

                var functions = await ListFunctionsConfigAsync();
                var triggers = GetTriggers(functions, tracer);
                if (Environment.IsAzureEnvironment())
                {
                    var client = new OperationClient(tracer);
                    await client.PostAsync("/operations/settriggers", triggers);
                }
            }
        }

        private static bool IsFunctionsSiteExtensionEnabled
        {
            get 
            {
                var functionVersion = System.Environment.GetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION");
                return !String.IsNullOrEmpty(functionVersion) &&
                       !String.Equals("disabled", functionVersion, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static bool FunctionIsDisabled(JObject functionConfig)
        {
            // Inspect the per function config values that are used to disable a function
            JToken value;
            if ((functionConfig.TryGetValue("disabled", out value) ||
                 functionConfig.TryGetValue("excluded", out value)) && (bool)value)
            {
                return true;
            }

            return false;
        }

        internal static JArray GetTriggers(IEnumerable<FunctionEnvelope> functions, ITracer tracer)
        {
            JArray triggers = new JArray();
            foreach (var function in functions)
            {
                try
                {
                    if (FunctionIsDisabled(function.Config))
                    {
                        tracer.Trace(String.Format("{0} is disabled", function));
                        continue;
                    }

                    foreach (JObject binding in function.Config.Value<JArray>("bindings"))
                    {
                        var type = binding.Value<string>("type");
                        if (type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))
                        {
                            binding.Add("functionName", function.Name);
                            tracer.Trace(String.Format("Syncing {0} of {1}", type, function.Name));
                            triggers.Add(binding);
                        }
                        else
                        {
                            tracer.Trace(String.Format("Skipping {0} of {1}", type, function.Name));
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracer.Trace(String.Format("{0} is invalid. {1}", function.Name, ex.Message));
                }
            }

            return triggers;
        }

        public async Task<FunctionEnvelope> CreateOrUpdateAsync(string name, FunctionEnvelope functionEnvelope, Action setConfigChanged)
        {
            var functionDir = Path.Combine(_environment.FunctionsPath, name);

            // Make sure the function folder exists
            if (!FileSystemHelpers.DirectoryExists(functionDir))
            {
                // Cleanup any leftover artifacts from a function with the same name before.
                DeleteFunction(name, ignoreErrors: true);
                FileSystemHelpers.EnsureDirectory(functionDir);
            }

            string newConfig = null;
            string configPath = Path.Combine(functionDir, Constants.FunctionsConfigFile);
            string dataFilePath = GetFunctionTestDataFilePath(name);

            // If files are included, write them out
            if (functionEnvelope?.Files != null)
            {
                // If the config is passed in the file collection, save it and don't process it as a file
                if (functionEnvelope.Files.TryGetValue(Constants.FunctionsConfigFile, out newConfig))
                {
                    functionEnvelope.Files.Remove(Constants.FunctionsConfigFile);
                }

                // Delete all existing files in the directory. This will also delete current function.json, but it gets recreated below
                FileSystemHelpers.DeleteDirectoryContentsSafe(functionDir);

                await Task.WhenAll(functionEnvelope.Files.Select(e => FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, e.Key), e.Value)));
            }

            // Get the config (if it was not already passed in as a file)
            if (newConfig == null && functionEnvelope?.Config != null)
            {
                newConfig = JsonConvert.SerializeObject(functionEnvelope?.Config, Formatting.Indented);
            }

            // Get the current config, if any
            string currentConfig = null;
            if (FileSystemHelpers.FileExists(configPath))
            {
                currentConfig = await FileSystemHelpers.ReadAllTextFromFileAsync(configPath);
            }

            // Save the file and set changed flag is it has changed. This helps optimize the syncTriggers call
            if (newConfig != currentConfig)
            {
                await FileSystemHelpers.WriteAllTextToFileAsync(configPath, newConfig);
                setConfigChanged();
            }

            if (functionEnvelope.TestData != null)
            {
                await FileSystemHelpers.WriteAllTextToFileAsync(dataFilePath, functionEnvelope.TestData);
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

        public async Task<FunctionSecrets> GetFunctionSecretsAsync(string functionName)
        {
            FunctionSecrets secrets;
            string secretFilePath = GetFunctionSecretsFilePath(functionName);
            if (FileSystemHelpers.FileExists(secretFilePath))
            {
                // load the secrets file
                string secretsJson = await FileSystemHelpers.ReadAllTextFromFileAsync(secretFilePath);
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
                await FileSystemHelpers.WriteAllTextToFileAsync(secretFilePath, JsonConvert.SerializeObject(secrets, Formatting.Indented));
            }

            secrets.TriggerUrl = String.Format(@"https://{0}/api/{1}?code={2}",
                System.Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost",
                functionName,
                secrets.Key);

            return secrets;
        }

        public async Task<JObject> GetHostConfigAsync()
        {
            var host = await TryGetHostConfigAsync();
            if (host == null)
            {
                throw new FileNotFoundException("Host.json is invalid");
            }
            return host;
        }

        private async Task<JObject> TryGetHostConfigAsync()
        {
            try
            {
                return FileSystemHelpers.FileExists(HostJsonPath)
                    ? JObject.Parse(await FileSystemHelpers.ReadAllTextFromFileAsync(HostJsonPath))
                    : new JObject();
            }
            catch
            {
                // no-op
            }

            return null;
        }

        public async Task<JObject> PutHostConfigAsync(JObject content)
        {
            await FileSystemHelpers.WriteAllTextToFileAsync(HostJsonPath, JsonConvert.SerializeObject(content));
            return await GetHostConfigAsync();
        }

        public void DeleteFunction(string name, bool ignoreErrors)
        {
            FileSystemHelpers.DeleteDirectorySafe(GetFunctionPath(name), ignoreErrors);
            FileSystemHelpers.DeleteFileSafe(GetFunctionTestDataFilePath(name));
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
            var functionConfig = JObject.Parse(configContent);

            return new FunctionEnvelope
            {
                Name = functionName,
                ScriptRootPathHref = FilePathToVfsUri(GetFunctionPath(functionName), isDirectory: true),
                ScriptHref = FilePathToVfsUri(GetFunctionScriptPath(functionName, functionConfig)),
                ConfigHref = FilePathToVfsUri(GetFunctionConfigPath(functionName)),
                SecretsFileHref = FilePathToVfsUri(GetFunctionSecretsFilePath(functionName)),
                Href = GetFunctionHref(functionName),
                Config = functionConfig,
                TestData = GetFunctionTestData(functionName)
            };
        }

        private string GetFunctionScriptPath(string functionName, JObject functionConfig)
        {
            var functionPath = GetFunctionPath(functionName);
            var functionFiles = FileSystemHelpers.GetFiles(functionPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(p => Path.GetFileName(p).ToLowerInvariant() != "function.json").ToArray();

            return DeterminePrimaryScriptFile(functionConfig, functionFiles);
        }

        // Logic for this function is copied from here:
        // https://github.com/Azure/azure-webjobs-sdk-script/blob/dev/src/WebJobs.Script/Host/ScriptHost.cs
        // These two implementations must stay in sync!
        internal static string DeterminePrimaryScriptFile(JObject functionConfig, string[] functionFiles)
        {
            if (functionFiles.Length == 1)
            {
                // if there is only a single file, that file is primary
                return functionFiles[0];
            }
            else
            {
                // First see if there is an explicit primary file indicated
                // in config. If so use that.
                string functionPrimary = null;
                string scriptFileName = (string)functionConfig["scriptFile"];
                if (!string.IsNullOrEmpty(scriptFileName))
                {
                    functionPrimary = functionFiles.FirstOrDefault(p =>
                        string.Compare(Path.GetFileName(p), scriptFileName, StringComparison.OrdinalIgnoreCase) == 0);
                }
                else
                {
                    // if there is a "run" file, that file is primary,
                    // for Node, any index.js file is primary
                    functionPrimary = functionFiles.FirstOrDefault(p =>
                        Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == "run" ||
                        Path.GetFileName(p).ToLowerInvariant() == "index.js");
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

        private string GetFunctionTestData(string functionName)
        {
            string testDataFilePath = GetFunctionTestDataFilePath(functionName);

            // Create an empty file if it doesn't exist
            if (!FileSystemHelpers.FileExists(testDataFilePath))
            {
                FileSystemHelpers.WriteAllText(testDataFilePath, String.Empty);
            }

            return FileSystemHelpers.ReadAllText(testDataFilePath);
        }

        private string GetFunctionTestDataFilePath(string functionName)
        {
            string folder = Path.Combine(_environment.DataPath, Constants.Functions, Constants.SampleData);
            FileSystemHelpers.EnsureDirectory(folder);
            return Path.Combine(folder, $"{functionName}.dat");
        }

        private string GetFunctionSecretsFilePath(string functionName)
        {
            return Path.Combine(_environment.DataPath, Constants.Functions, Constants.Secrets, $"{functionName}.json");
        }
    }
}