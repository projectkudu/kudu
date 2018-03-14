using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using NuGet.Versioning;

namespace Kudu.Core.Deployment.Generator
{
    //  Site builder class hierarchy:
    //
    //  ExternalCommandBuilder
    //      CustomBuilder
    //      GeneratorSiteBuilder
    //          BaseBasicBuilder
    //              BasicBuilder
    //              NodeSiteBuilder
    //          WapBuilder
    //          WebSiteBuilder
    //          DotNetConsoleBuilder
    //          CustomGeneratorCommandSiteBuilder

    public abstract class ExternalCommandBuilder : ISiteBuilder
    {
        private const string PostDeploymentActions = "PostDeploymentActions";

        protected ExternalCommandBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath)
        {
            Environment = environment;

            DeploymentSettings = settings;
            RepositoryPath = repositoryPath;
            PropertyProvider = propertyProvider;

            ExternalCommandFactory = new ExternalCommandFactory(environment, settings, repositoryPath);
        }

        public abstract string ProjectType { get; }

        protected IEnvironment Environment { get; private set; }

        protected IDeploymentSettingsManager DeploymentSettings { get; private set; }

        protected string RepositoryPath { get; private set; }

        protected IBuildPropertyProvider PropertyProvider { get; private set; }

        internal ExternalCommandFactory ExternalCommandFactory { get; private set; }

        public abstract Task Build(DeploymentContext context);

        public void PostBuild(DeploymentContext context)
        {
            context.Logger.Log("Running post deployment command(s)...");
            foreach (var file in GetPostBuildActionScripts())
            {
                var fi = new FileInfo(file);
                string scriptFilePath = null;
                if (string.Equals(".ps1", fi.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    scriptFilePath = string.Format(CultureInfo.InvariantCulture, "PowerShell.exe -ExecutionPolicy RemoteSigned -File \"{0}\"", file);
                }
                else
                {
                    scriptFilePath = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", file);
                }
                RunCommand(context, scriptFilePath, false, Path.GetFileNameWithoutExtension(file));
            }
        }

        protected void RunCommand(DeploymentContext context, string command, bool ignoreManifest, string message = "Running deployment command...")
        {
            ILogger customLogger = context.Logger.Log(message);
            customLogger.Log("Command: " + command);

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = ExternalCommandFactory.BuildExternalCommandExecutable(RepositoryPath, context.OutputPath, customLogger);

            exe.EnvironmentVariables[WellKnownEnvironmentVariables.PreviousManifestPath] = context.PreviousManifestFilePath ?? String.Empty;
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NextManifestPath] = context.NextManifestFilePath;
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.IgnoreManifest] = ignoreManifest ? "1" : "0";

            exe.EnvironmentVariables[WellKnownEnvironmentVariables.BuildTempPath] = context.BuildTempPath;

            // Set Commit ID in the environment
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.CommitId] = context.CommitId;
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.CommitMessage] = context.Message;

            // Populate the environment with the build properties
            foreach (var property in PropertyProvider.GetProperties())
            {
                exe.EnvironmentVariables[property.Key] = property.Value;
            }

            try
            {
                exe.ExecuteWithProgressWriter(customLogger, context.Tracer, command, String.Empty);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                throw;
            }
        }

        /// <summary>
        /// Mark public for testing
        /// </summary>
        public IList<string> GetPostBuildActionScripts()
        {
            List<string> scriptFilesGroupedAndSorted = new List<string>();
            List<string> scriptFolders = new List<string>();

            const string extensionEnvVarSuffix = "_EXTENSION_VERSION";

            // "/site/deployments/tools/PostDeploymentActions" (can override with %SCM_POST_DEPLOYMENT_ACTIONS_PATH%)
            // if %SCM_POST_DEPLOYMENT_ACTIONS_PATH% is set, we will support both absolute path or relative parth from "d:/home/site/repository"
            var customPostDeploymentPath = DeploymentSettings.GetValue(SettingsKeys.PostDeploymentActionsDirectory);
            var postDeploymentPath = string.IsNullOrEmpty(customPostDeploymentPath) ? Path.Combine(Environment.DeploymentToolsPath, PostDeploymentActions) : Path.Combine(Environment.RepositoryPath, customPostDeploymentPath);

            if (FileSystemHelpers.DirectoryExists(postDeploymentPath))
            {
                scriptFolders.Add(postDeploymentPath);
            }

            // D:\Program Files (x86)\SiteExtensions
            string siteExtensionFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "SiteExtensions");
            // Find all enviroment variable with format {package id}_EXTENSION_VERSION,
            // And find scripts from PostDeploymentActions" folder
            foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                string key = entry.Key.ToString();
                string val = entry.Value.ToString();
                NuGetVersion ver = null;
                if (key.EndsWith(extensionEnvVarSuffix, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals("latest", val, StringComparison.OrdinalIgnoreCase) || NuGetVersion.TryParse(val, out ver)))   // either latest or a specific version
                {
                    string extensionName = key.Substring(0, key.Length - extensionEnvVarSuffix.Length);
                    if (ver != null)
                    {
                        // "D:\Program Files (x86)\SiteExtensions", "{ExtensionName}", "{version}", "PostDeploymentActions"
                        //  => D:\Program Files (x86)\SiteExtensions\{ExtensionName}\{version}\PostDeploymentActions
                        string scriptFolder = Path.Combine(siteExtensionFolder, extensionName, ver.ToNormalizedString(), PostDeploymentActions);
                        if (FileSystemHelpers.DirectoryExists(scriptFolder))
                        {
                            scriptFolders.Add(scriptFolder);
                        }
                    }
                    else
                    {
                        // Get the latest version
                        // "D:\Program Files (x86)\SiteExtensions", "{PackageName}"
                        //  => D:\Program Files (x86)\SiteExtensions\{PackageName}
                        string packageRoot = Path.Combine(siteExtensionFolder, extensionName);
                        if (FileSystemHelpers.DirectoryExists(packageRoot))
                        {
                            string[] allVersions = FileSystemHelpers.GetDirectories(packageRoot);   // expecting a list of version e.g D:\Program Files (x86)\SiteExtensions\{PackageName}\{version}
                            if (allVersions.Count() > 0)
                            {
                                // latest version comes first
                                Array.Sort(allVersions, (string v1, string v2) =>
                                {
                                    NuGetVersion ver1 = NuGetVersion.Parse(new DirectoryInfo(v1).Name);
                                    NuGetVersion ver2 = NuGetVersion.Parse(new DirectoryInfo(v2).Name);
                                    return ver2.CompareTo(ver1);
                                });

                                // "D:\Program Files (x86)\SiteExtensions\{PackageName}\{version}", "PostDeploymentActions"
                                // => D:\Program Files (x86)\SiteExtensions\{PackageName}\{version}\PostDeploymentActions
                                string scriptFolder = Path.Combine(allVersions[0], PostDeploymentActions);
                                if (FileSystemHelpers.DirectoryExists(scriptFolder))
                                {
                                    scriptFolders.Add(scriptFolder);
                                }
                            }
                        }
                    }
                }
            }

            // Find all post action scripts and order file alphabetically for each folder
            foreach (var sf in scriptFolders)
            {
                var files = FileSystemHelpers.GetFiles(sf, "*")
                                        .Where(f => f.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                                            || f.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                                            || f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)).ToList();

                files.Sort();
                scriptFilesGroupedAndSorted.AddRange(files);
            }

            return scriptFilesGroupedAndSorted;
        }
    }
}
