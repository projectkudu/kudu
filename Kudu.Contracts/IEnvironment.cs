namespace Kudu.Core
{
    public interface IEnvironment
    {
        string RootPath { get; }                // e.g. /
        string SiteRootPath { get; }            // e.g. /site
        string RepositoryPath { get; set; }     // e.g. /site/repository
        string WebRootPath { get; }             // e.g. /site/wwwroot
        string DeploymentsPath { get; }         // e.g. /site/deployments
        string DeploymentToolsPath { get; }     // e.g. /site/deployments/tools
        string SiteExtensionSettingsPath { get; }     // e.g. /site/siteextensions
        string DiagnosticsPath { get; }         // e.g. /site/diagnostics
        string LocksPath { get; }               // e.g. /site/locks
        string SSHKeyPath { get; }
        string TempPath { get; }
        string ZipTempPath { get; }             // e.g. ${TempPath}/zipdeploy
        string ScriptPath { get; }
        string NodeModulesPath { get; }
        string LogFilesPath { get; }            // e.g. /logfiles
        string ApplicationLogFilesPath { get; } // e.g. /logfiles/application
        string TracePath { get; }               // e.g. /logfiles/kudu/trace
        string AnalyticsPath { get; }           // e.g. %temp%/siteExtLogs
        string DeploymentTracePath { get; }     // e.g. /logfiles/kudu/deployment
        string DataPath { get; }                // e.g. /data
        string JobsDataPath { get; }            // e.g. /data/jobs
        string JobsBinariesPath { get; }        // e.g. /site/wwwroot/app_data/jobs
        string SecondaryJobsBinariesPath { get; } // e.g. /site/jobs
        string FunctionsPath { get; }           // e.g. /site/wwwroot
        string AppBaseUrlPrefix { get; }        // e.g. siteName.azurewebsites.net
        string RequestId { get; }               // e.g. x-arr-log-id or x-ms-request-id header value
        string SitePackagesPath { get; }        // e.g. /data/SitePackages
    }
}
