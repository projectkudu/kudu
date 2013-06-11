using System;

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
        public const string TraceLockFile = "trace.lock";
        public const string NpmDebugLogFile = "npm-debug.log";

        public const string DeploymentCachePath = "deployments";
        public const string DeploymentToolsPath = "tools";
        public const string LogFilesPath = @"LogFiles";
        public const string TracePath = LogFilesPath + @"\Git\trace";
        public const string DeploySettingsPath = "settings.xml";
        public const string ActiveDeploymentFile = "active";
        public const string TraceFile = "trace.xml";
        public const string ScriptsPath = "scripts";
        public const string NodeModulesPath = "node_modules";
        public const string FirstDeploymentManifestFileName = "firstDeploymentManifest";

        // Kudu trace text file related
        public const string DeploymentTracePath = LogFilesPath + @"\Git\deployment";

        public const string TraceFileFormat = "{0}-{1}.txt";
        public const string TraceFileEnvKey = "KUDU_TRACE_FILE";

        public const string DiagnosticsPath = @"diagnostics";
        public const string SettingsJsonFile = @"settings.json";

        public const string HostingStartHtml = "hostingstart.html";

        private static readonly TimeSpan _maxAllowedExectionTime = TimeSpan.FromMinutes(30);

        public static TimeSpan MaxAllowedExecutionTime
        {
            get { return _maxAllowedExectionTime; }
        }
    }
}