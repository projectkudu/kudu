using System;
using System.IO;

namespace Kudu
{
    public static class Constants
    {
        public const string WebRoot = "wwwroot";
        public const string MappedSite = "/_app";
        public const string RepositoryPath = "repository";

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
        public const string ScriptsPath = "scripts";
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

        private static readonly TimeSpan _maxAllowedExectionTime = TimeSpan.FromMinutes(30);

        public static TimeSpan MaxAllowedExecutionTime
        {
            get { return _maxAllowedExectionTime; }
        }

        public const string ApplicationHostXdtFileName = "applicationHost.xdt";

        public const string RequestIdHeader = "x-ms-request-id";

        public const string SiteOperationHeaderKey = "X-MS-SITE-OPERATION";
        public const string SiteOperationRestart = "restart";

        public const string SiteExtensionProvisioningStateCreated = "Created";
        public const string SiteExtensionProvisioningStateAccepted = "Accepted";
        public const string SiteExtensionProvisioningStateSucceeded = "Succeeded";
        public const string SiteExtensionProvisioningStateFailed = "Failed";
        public const string SiteExtensionProvisioningStateCanceled = "Canceled";

        public const string SiteExtensionOperationInstall = "install";

        // TODO: need localization?
        public const string SiteExtensionProvisioningStateNotFoundMessageFormat = "'{0}' not found.";
        public const string SiteExtensionProvisioningStateDownloadFailureMessageFormat = "'{0}' download failure.";
        public const string SiteExtensionProvisioningStateInvalidPackageMessageFormat = "Invalid '{0}' package.";

        public const string FreeSKU = "Free";
        public const string BasicSKU = "Basic";

        //Setting for VC++ for node builds
        public const string VCVersion = "2015";

        // Enviroment variable for site retricted jwt token
        public const string SiteRestrictedJWT = "X-MS-SITE-RESTRICTED-JWT";
        public const string HttpHost = "HTTP_HOST";

        public const string Function = "function";
        public const string Functions = "functions";
        public const string FunctionsConfigFile = "function.json";
        public const string FunctionsHostConfigFile = "host.json";
        public const string Secrets = "secrets";
        public const string SampleData = "sampledata";
        public const string FunctionsPortal = "FunctionsPortal";
    }
}