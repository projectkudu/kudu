using System;
using System.IO;

namespace Kudu
{
    public static class Constants
    {
        public const string WebRoot = "wwwroot";
        public const string MappedSite = "/_app";
        public const string RepositoryPath = "repository";
        public const string ZipTempDirectoryName = "zipdeploy";
        public const string ArtifactStagingDirectoryName = "extracted";

        public const string LockPath = "locks";
        public const string DeploymentLockFile = "deployments.lock";
        public const string StatusLockFile = "status.lock";
        public const string SSHKeyLockFile = "sshkey.lock";
        public const string HooksLockFile = "hooks.lock";
        public const string SSHKeyPath = ".ssh";
        public const string NpmDebugLogFile = "npm-debug.log";

        public const string DeploymentCachePath = "deployments";
        public const string SiteExtensionsCachePath = "siteextensions";
        public const string DeploymentToolsPath = "tools";
        public const string SiteFolder = @"site";
        public const string LogFilesPath = @"LogFiles";
        public const string ApplicationLogFilesDirectory = "Application";
        public readonly static string TracePath = Path.Combine(LogFilesPath, "kudu", "trace");
        public const string SiteExtensionLogsDirectory = "siteExtLogs";
        public const string DeploySettingsPath = "settings.xml";
        public const string ActiveDeploymentFile = "active";
        public const string ScriptsPath = "Scripts";
        public const string NodeModulesPath = "node_modules";
        public const string FirstDeploymentManifestFileName = "firstDeploymentManifest";
        public const string ManifestFileName = "manifest";

        public const string AppDataPath = "App_Data";
        public const string DataPath = "data";
        public const string JobsPath = "jobs";
        public const string ContinuousPath = "continuous";
        public const string TriggeredPath = "triggered";

        public const string DummyRazorExtension = ".kudu777";

        // Kudu trace text file related
        public const string DeploymentTracePath = LogFilesPath + @"\kudu\deployment";

        public const string TraceFileFormat = "{0}-{1}.txt";
        public const string TraceFileEnvKey = "KUDU_TRACE_FILE";

        public const string DiagnosticsPath = @"diagnostics";
        public const string LocksPath = @"locks";
        public const string SettingsJsonFile = @"settings.json";

        public const string HostingStartHtml = "hostingstart.html";

        public const string DnxDefaultVersion = "1.0.0-rc1-final";
        public const string DnxDefaultClr = "CLR";

        // These should match the ones that are set by Azure
        public const string X64Bit = "AMD64";
        public const string X86Bit = "x86";

        public const string LatestDeployment = "latest";

        private static readonly TimeSpan _maxAllowedExecutionTime = TimeSpan.FromMinutes(30);

        public static TimeSpan MaxAllowedExecutionTime
        {
            get { return _maxAllowedExecutionTime; }
        }

        public const string ApplicationHostXdtFileName = "applicationHost.xdt";
        public const string ScmApplicationHostXdtFileName = "scmApplicationHost.xdt";

        public const string ArrLogIdHeader = "x-arr-log-id";
        public const string RequestIdHeader = "x-ms-request-id";
        public const string ClientRequestIdHeader = "x-ms-client-request-id";
        public const string RequestDateTimeUtc = "RequestDateTimeUtc";
        public const string ScmDeploymentKind = "ScmDeploymentKind";

        public const string SiteOperationHeaderKey = "X-MS-SITE-OPERATION";
        public const string SiteOperationRestart = "restart";

        public const string LogicAppJson = "logicapp.json";
        public const string LogicAppUrlKey = "LOGICAPP_URL";

        public const string RestartApiPath = "/api/app/restart";

        public const string SiteExtensionProvisioningStateCreated = "Created";
        public const string SiteExtensionProvisioningStateAccepted = "Accepted";
        public const string SiteExtensionProvisioningStateSucceeded = "Succeeded";
        public const string SiteExtensionProvisioningStateFailed = "Failed";
        public const string SiteExtensionProvisioningStateCanceled = "Canceled";

        public const string SiteExtensionOperationInstall = "install";

        // TODO: need localization?
        public const string SiteExtensionProvisioningStateNotFoundMessageFormat = "'{0}' not found.";

        public const string FreeSKU = "Free";
        public const string BasicSKU = "Basic";

        // Setting for VC++ for node builds
        public const string VCVersion = "2015";

        public const string RoleBasedContributorHeader = "X-MS-CLIENT-ROLEBASED-CONTRIBUTOR";
        public const string ClientAuthorizationSourceHeader = "X-MS-CLIENT-AUTHORIZATION-SOURCE";
        public const string SiteRestrictedToken = "x-ms-site-restricted-token";
        public const string SiteAuthEncryptionKey = "WEBSITE_AUTH_ENCRYPTION_KEY";
        public const string HttpHost = "HTTP_HOST";
        public const string HttpAuthority = "HTTP_AUTHORITY";
        public const string WebSiteSwapSlotName = "WEBSITE_SWAP_SLOTNAME";

        public const string Function = "function";
        public const string Functions = "functions";
        public const string FunctionsConfigFile = "function.json";
        public const string FunctionsHostConfigFile = "host.json";
        public const string ProxyConfigFile = "proxies.json";
        public const string Secrets = "secrets";
        public const string SampleData = "sampledata";
        public const string FunctionsPortal = "FunctionsPortal";
        public const string FunctionKeyNewFormat = "~0.7";
        public const string FunctionRunTimeVersion = "FUNCTIONS_EXTENSION_VERSION";
        public const string WebSiteSku = "WEBSITE_SKU";
        public const string WebSiteElasticScaleEnabled = "WEBSITE_ELASTIC_SCALING_ENABLED";
        public const string DynamicSku = "Dynamic";
        public const string ElasticScaleEnabled = "1";
        public const string AzureWebJobsSecretStorageType = "AzureWebJobsSecretStorageType";
        public const string HubName = "HubName";
        public const string DurableTaskStorageConnection = "connection";
        public const string DurableTaskStorageConnectionName = "azureStorageConnectionStringName";
        public const string DurableTask = "durableTask";
        public const string Extensions = "extensions";
        public const string SitePackages = "SitePackages";
        public const string PackageNameTxt = "packagename.txt";
        public const string AppOfflineFileName = "app_offline.htm";
        public const string AppOfflineKuduContent = "Created by kudu";

        public const string OneDeploy = "OneDeploy";
    }
}