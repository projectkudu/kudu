using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Functions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                await PostDeploymentHelper.SyncFunctionsTriggers(_environment.RequestId, _environment.SiteRestrictedJwt, new PostDeploymentTraceListener(tracer));
            }
        }

        private static string FunctionSiteExtensionVersion
        {
            get
            {
                return System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion);
            }
        }

        public async Task<FunctionEnvelope> CreateOrUpdateAsync(string name, FunctionEnvelope functionEnvelope, Action setConfigChanged)
        {
            var functionDir = Path.Combine(_environment.FunctionsPath, name);

            // Make sure the function folder exists
            if (!FileSystemHelpers.DirectoryExists(functionDir))
            {
                // Cleanup any leftover artifacts from a function with the same name before.
                DeleteFunctionArtifacts(name);
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

            return await GetFunctionConfigAsync(name); // test_data took from incoming request, it will not exceed the limit
        }

        public async Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfigAsync(FunctionTestData packageLimit = null) // null means no limit
        {
            var configList = await Task.WhenAll(
                    FileSystemHelpers
                    .GetDirectories(_environment.FunctionsPath)
                    .Select(d => TryGetFunctionConfigAsync(Path.GetFileName(d), packageLimit)));
            // TryGetFunctionConfigAsync checks the existence of function.json
            return configList.Where(c => c != null);
        }

        public async Task<FunctionEnvelope> GetFunctionConfigAsync(string name, FunctionTestData packageLimit = null) // null means no limit
        {
            var config = await TryGetFunctionConfigAsync(name, packageLimit);
            if (config == null)
            {
                throw new FileNotFoundException($"Function ({name}) config does not exist or is invalid");
            }
            return config;
        }

        private async Task<T> GetKeyObjectFromFile<T>(string name, IKeyJsonOps<T> keyOp)
        {
            var secretStorageType = System.Environment.GetEnvironmentVariable(Constants.AzureWebJobsSecretStorageType);
            if (!string.IsNullOrEmpty(secretStorageType) &&
                secretStorageType.Equals("Blob", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Runtime keys are stored on blob storage. This API doesn't support this configuration.");
            }

            string keyPath = GetFunctionSecretsFilePath(name);
            string key = null;
            if (!FileSystemHelpers.FileExists(keyPath) || FileSystemHelpers.FileInfoFromFileName(keyPath).Length == 0)
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(keyPath));
                try
                {
                    using (var fileStream = FileSystemHelpers.OpenFile(keyPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    // getting the lock early (instead of acquire the lock at "new StreamWriter(fileStream)")
                    // so no redundant work is being done (generate secrets)
                    {
                        string jsonContent = keyOp.GenerateKeyJson(SecurityUtility.GenerateSecretStringsKeyPair(keyOp.NumberOfKeysInDefaultFormat), FunctionSiteExtensionVersion, out key);
                        using (var sw = new StringWriter())
                        using (var sr = new System.IO.StringReader(jsonContent))
                        {
                            // write json to memory
                            // since JsonConvert has no method to format a json string
                            new JsonTextWriter(sw) { Formatting = Formatting.Indented }.WriteToken(new JsonTextReader(sr));
                            using (var streamWriter = new StreamWriter(fileStream))
                            {
                                await streamWriter.WriteAsync(sw.ToString());
                                await streamWriter.FlushAsync();
                            }
                        }
                    }
                    return keyOp.GenerateKeyObject(key, name);
                }
                catch (IOException)
                {
                    // failed to open file => function runtime has the handler
                    // fallback to read key files
                }
            }

            string jsonStr = null;
            int timeOut = 5;
            while (true)
            {
                try
                {
                    jsonStr = await FileSystemHelpers.ReadAllTextFromFileAsync(keyPath);
                    break;
                }
                catch (Exception)
                {
                    if (timeOut == 0)
                    {
                        throw new TimeoutException($"Fail to read {keyPath}, the file is being held by another process");
                    }
                    timeOut--;
                    await Task.Delay(250);
                }
            }

            bool isEncrypted;
            key = keyOp.GetKeyValueFromJson(jsonStr, out isEncrypted);
            if (isEncrypted)
            {
                key = SecurityUtility.DecryptSecretString(key);
            }
            return keyOp.GenerateKeyObject(key, name);

        }

        public string GetAdminToken() => SecurityUtility.GenerateFunctionToken();

        public async Task<MasterKey> GetMasterKeyAsync()
        {
            return await GetKeyObjectFromFile<MasterKey>("host", new MasterKeyJsonOps());
        }

        public async Task<FunctionSecrets> GetFunctionSecretsAsync(string functionName)
        {
            // check to see if the function folder exists
            GetFuncPathAndCheckExistence(functionName);
            return await GetKeyObjectFromFile<FunctionSecrets>(functionName, new FunctionSecretsJsonOps());
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
            FileSystemHelpers.DeleteDirectorySafe(GetFuncPathAndCheckExistence(name), ignoreErrors);
            DeleteFunctionArtifacts(name);
        }

        private void DeleteFunctionArtifacts(string name)
        {
            FileSystemHelpers.DeleteFileSafe(GetFunctionTestDataFilePath(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionSecretsFilePath(name));
            FileSystemHelpers.DeleteFileSafe(GetFunctionLogPath(name));
        }

        private async Task<FunctionEnvelope> TryGetFunctionConfigAsync(string name, FunctionTestData packageLimit)
        {
            try
            {
                var path = GetFunctionConfigPath(name);
                if (FileSystemHelpers.FileExists(path))
                {
                    return await CreateFunctionConfig(await FileSystemHelpers.ReadAllTextFromFileAsync(path), name, packageLimit);
                }
            }
            catch
            {
                // no-op
            }
            return null;
        }

        private async Task<FunctionEnvelope> CreateFunctionConfig(string configContent, string functionName, FunctionTestData packageLimit)
        {
            var functionConfig = JObject.Parse(configContent);
            var functionPath = GetFuncPathAndCheckExistence(functionName);

            return new FunctionEnvelope
            {
                Name = functionName,
                ScriptRootPathHref = FilePathToVfsUri(functionPath, isDirectory: true),
                ScriptHref = FilePathToVfsUri(DeterminePrimaryScriptFile(functionConfig, functionPath)),
                ConfigHref = FilePathToVfsUri(GetFunctionConfigPath(functionName)),
                SecretsFileHref = FilePathToVfsUri(GetFunctionSecretsFilePath(functionName)),
                Href = GetFunctionHref(functionName),
                Config = functionConfig,
                TestData = await GetFunctionTestData(functionName, packageLimit)
            };
        }

        private void TraceAndThrowError(Exception e)
        {
            var tracer = _traceFactory.GetTracer();
            tracer.TraceError(e);
            throw e;
        }

        // Logic for this function is copied from here:
        // https://github.com/Azure/azure-webjobs-sdk-script/blob/dev/src/WebJobs.Script/Host/ScriptHost.cs
        // These two implementations must stay in sync!

        /// <summary>
        /// Determines which script should be considered the "primary" entry point script.
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">Thrown if the function metadata points to an invalid script file, or no script files are present.</exception>
        internal string DeterminePrimaryScriptFile(JObject functionConfig, string scriptDirectory)
        {
            // First see if there is an explicit primary file indicated
            // in config. If so use that.
            string functionPrimary = null;
            string scriptFile = (string)functionConfig["scriptFile"];

            if (!string.IsNullOrEmpty(scriptFile))
            {
                string scriptPath = Path.Combine(scriptDirectory, scriptFile);
                if (!FileSystemHelpers.FileExists(scriptPath))
                {
                    TraceAndThrowError(new ConfigurationErrorsException("Invalid script file name configuration. The 'scriptFile' property is set to a file that does not exist."));
                }

                functionPrimary = scriptPath;
            }
            else
            {
                string[] functionFiles = FileSystemHelpers.GetFiles(scriptDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(p => !String.Equals(Path.GetFileName(p), "function.json", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (functionFiles.Length == 0)
                {
                    TraceAndThrowError(new ConfigurationErrorsException("No function script files present."));
                }

                if (functionFiles.Length == 1)
                {
                    // if there is only a single file, that file is primary
                    functionPrimary = functionFiles[0];
                }
                else
                {
                    // if there is a "run" file, that file is primary,
                    // for Node, any index.js file is primary
                    functionPrimary = functionFiles.FirstOrDefault(p =>
                        String.Equals(Path.GetFileNameWithoutExtension(p), "run", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(Path.GetFileName(p), "index.js", StringComparison.OrdinalIgnoreCase));
                }
            }

            if (string.IsNullOrEmpty(functionPrimary))
            {
                TraceAndThrowError(new ConfigurationErrorsException("Unable to determine the primary function script. Try renaming your entry point script to 'run' (or 'index' in the case of Node), " +
                    "or alternatively you can specify the name of the entry point script explicitly by adding a 'scriptFile' property to your function metadata."));
            }

            return Path.GetFullPath(functionPrimary);
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

        private string GetFuncPathAndCheckExistence(string name)
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
            return Path.Combine(GetFuncPathAndCheckExistence(name), Constants.FunctionsConfigFile);
        }

        private string GetFunctionLogPath(string name)
        {
            return Path.Combine(_environment.ApplicationLogFilesPath, Constants.Functions, Constants.Function, name);
        }

        private async Task<string> GetFunctionTestData(string functionName, FunctionTestData packageLimit)
        {
            string testDataFilePath = GetFunctionTestDataFilePath(functionName);

            // Create an empty file if it doesn't exist
            if (!FileSystemHelpers.FileExists(testDataFilePath))
            {
                FileSystemHelpers.WriteAllText(testDataFilePath, String.Empty);
            }

            if (packageLimit != null)
            {
                var fileSize = FileSystemHelpers.FileInfoFromFileName(testDataFilePath).Length;
                if (!packageLimit.DeductFromBytesLeftInPackage(fileSize))
                {
                    return $"Test_Data is of size {fileSize} bytes, but there's only {packageLimit.BytesLeftInPackage} bytes left in ARM response";
                }
            }

            return await FileSystemHelpers.ReadAllTextFromFileAsync(testDataFilePath);

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

        /// <summary>
        /// Populates a <see cref="ZipArchive"/> with the content of the function app.
        /// It can also include local.settings.json and .csproj files for a full Visual Studio project.
        /// sln file is not included since it changes between VS versions and VS can auto-generate it from the csproj.
        /// All existing functions are added as content with "Always" copy to output.
        /// </summary>
        /// <param name="zip">the <see cref="ZipArchive"/> to be populated with function app content.</param>
        /// <param name="includeAppSettings">Optional: indicates whether to add local.settings.json or not to the archive. Default is false.</param>
        /// <param name="includeCsproj">Optional: indicates whether to add a .csproj to the archive. Default is false.</param>
        /// <param name="projectName">Optional: the name for *.csproj file if <paramref name="includeCsproj"/> is true. Default is appName.</param>
        public void CreateArchive(ZipArchive zip, bool includeAppSettings = false, bool includeCsproj = false, string projectName = null)
        {
            var tracer = _traceFactory.GetTracer();
            var directoryInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(_environment.FunctionsPath);

            // First add the entire wwwroot folder at the root of the zip.
            IList<ZipArchiveEntry> files;
            zip.AddDirectory(directoryInfo, tracer, string.Empty, out files);

            if (includeAppSettings)
            {
                // include local.settings.json if needed.
                files.Add(AddAppSettingsFile(zip));
            }

            if (includeCsproj)
            {
                // include .csproj for Visual Studio if needed.
                projectName = projectName ?? ServerConfiguration.GetApplicationName();
                AddCsprojFile(zip, files, projectName);
            }
        }

        /// <summary>
        /// Creates a csproj file and adds it to the passed in ZipArchive
        /// The csproj contain references to the core SDK, Http trigger, and build task
        /// it also contain 'Always' copy entries for all the files that are in wwwroot.
        /// </summary>
        /// <param name="zip"><see cref="ZipArchive"/> to add csproj file to.</param>
        /// <param name="files"><see cref="IEnumerable{ZipArchiveEntry}"/> of entries in the zip file to include in the csproj.</param>
        /// <param name="projectName">the {projectName}.csproj</param>
        private static ZipArchiveEntry AddCsprojFile(ZipArchive zip, IEnumerable<ZipArchiveEntry> files, string projectName)
        {
            const string microsoftAzureWebJobsVersion = "2.1.0-beta1";
            const string microsoftAzureWebJobsExtensionsHttpVersion = "1.0.0-beta1";
            const string microsoftNETSdkFunctionsVersion = "1.0.0-alpha6";

            var csprojFile = new StringBuilder();
            csprojFile.AppendLine(
                $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net461</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Azure.WebJobs"" Version=""{microsoftAzureWebJobsVersion}"" />
    <PackageReference Include=""Microsoft.Azure.WebJobs.Extensions.Http"" Version=""{microsoftAzureWebJobsExtensionsHttpVersion}"" />
    <PackageReference Include=""Microsoft.NET.Sdk.Functions"" Version=""{microsoftNETSdkFunctionsVersion}"" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include=""Microsoft.CSharp"" />
  </ItemGroup>");

            csprojFile.AppendLine("  <ItemGroup>");
            foreach (var entry in files)
            {
                csprojFile.AppendLine(
                    $@"    <None Update=""{entry.FullName}"">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>");
            }
            csprojFile.AppendLine("  </ItemGroup>");
            csprojFile.AppendLine("</Project>");

            return zip.AddFile($"{projectName}.csproj", csprojFile.ToString());
        }

        /// <summary>
        /// creates a local.settings.json file and populates it with the values in AppSettings
        /// The AppSettings are read from EnvVars with prefix APPSETTING_.
        /// local.settings.json looks like:
        /// {
        ///   "IsEncrypted": true|false,
        ///   "Values": {
        ///     "Name": "Value"
        ///   }
        /// }
        /// This method doesn't include Connection Strings. Unlike AppSettings, connection strings
        /// have 10 different prefixes depending on the type.
        /// </summary>
        /// <param name="zip"><see cref="ZipArchive"/> to add local.settings.json file to.</param>
        /// <returns></returns>
        private static ZipArchiveEntry AddAppSettingsFile(ZipArchive zip)
        {
            const string appSettingsPrefix = "APPSETTING_";
            const string localAppSettingsFileName = "local.settings.json";

            var appSettings = System.Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .Where(p => p.Key.ToString().StartsWith(appSettingsPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(k => k.Key.ToString().Substring(appSettingsPrefix.Length), v => v.Value);

            var localSettings = JsonConvert.SerializeObject(new { IsEncrypted = false, Values = appSettings }, Formatting.Indented);

            return zip.AddFile(localAppSettingsFileName, localSettings);
        }
    }
}