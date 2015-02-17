using System;
using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Core
{
    public class Environment : IEnvironment
    {
        private readonly string _webRootPath;
        private readonly string _deploymentsPath;
        private readonly string _deploymentToolsPath;
        private readonly string _siteExtensionSettingsPath;
        private readonly string _diagnosticsPath;
        private readonly string _locksPath;
        private readonly string _sshKeyPath;
        private readonly string _tempPath;
        private readonly string _scriptPath;
        private readonly string _nodeModulesPath;
        private string _repositoryPath;
        private readonly string _logFilesPath;
        private readonly string _applicationLogFilesPath;
        private readonly string _tracePath;
        private readonly string _analyticsPath;
        private readonly string _deploymentTracePath;
        private readonly string _dataPath;
        private readonly string _jobsDataPath;
        private readonly string _jobsBinariesPath;

        // This ctor is used only in unit tests
        public Environment(
                string rootPath,
                string siteRootPath,
                string tempPath,
                string repositoryPath,
                string webRootPath,
                string deploymentsPath,
                string diagnosticsPath,
                string locksPath,
                string sshKeyPath,
                string scriptPath,
                string nodeModulesPath,
                string dataPath,
                string siteExtensionSettingsPath)
        {
            if (repositoryPath == null)
            {
                throw new ArgumentNullException("repositoryPath");
            }

            RootPath = rootPath;
            SiteRootPath = siteRootPath;
            _tempPath = tempPath;
            _repositoryPath = repositoryPath;
            _webRootPath = webRootPath;
            _deploymentsPath = deploymentsPath;
            _deploymentToolsPath = Path.Combine(_deploymentsPath, Constants.DeploymentToolsPath);
            _siteExtensionSettingsPath = siteExtensionSettingsPath;
            _diagnosticsPath = diagnosticsPath;
            _locksPath = locksPath;
            _sshKeyPath = sshKeyPath;
            _scriptPath = scriptPath;
            _nodeModulesPath = nodeModulesPath;

            _dataPath = dataPath;

            _jobsDataPath = Path.Combine(_dataPath, Constants.JobsPath);
            _jobsBinariesPath = _jobsDataPath;

            _logFilesPath = Path.Combine(rootPath, Constants.LogFilesPath);
            _applicationLogFilesPath = Path.Combine(_logFilesPath, Constants.ApplicationLogFilesDirectory);
            _tracePath = Path.Combine(rootPath, Constants.TracePath);
            _analyticsPath = Path.Combine(tempPath ?? _logFilesPath, Constants.SiteExtensionLogsDirectory);
            _deploymentTracePath = Path.Combine(rootPath, Constants.DeploymentTracePath);
        }

        public Environment(
                string rootPath,
                string binPath,
                string repositoryPath)
        {
            RootPath = rootPath;

            SiteRootPath = Path.Combine(rootPath, Constants.SiteFolder);

            _tempPath = Path.GetTempPath();
            _repositoryPath = repositoryPath;
            _webRootPath = Path.Combine(SiteRootPath, Constants.WebRoot);
            _deploymentsPath = Path.Combine(SiteRootPath, Constants.DeploymentCachePath);
            _deploymentToolsPath = Path.Combine(_deploymentsPath, Constants.DeploymentToolsPath);
            _siteExtensionSettingsPath = Path.Combine(SiteRootPath, Constants.SiteExtensionsCachePath);
            _diagnosticsPath = Path.Combine(SiteRootPath, Constants.DiagnosticsPath);
            _locksPath = Path.Combine(SiteRootPath, Constants.LocksPath);
            _sshKeyPath = Path.Combine(rootPath, Constants.SSHKeyPath);
            _scriptPath = Path.Combine(binPath, Constants.ScriptsPath);
            _nodeModulesPath = Path.Combine(binPath, Constants.NodeModulesPath);
            _logFilesPath = Path.Combine(rootPath, Constants.LogFilesPath);
            _applicationLogFilesPath = Path.Combine(_logFilesPath, Constants.ApplicationLogFilesDirectory);
            _tracePath = Path.Combine(rootPath, Constants.TracePath);
            _analyticsPath = Path.Combine(_tempPath ?? _logFilesPath, Constants.SiteExtensionLogsDirectory);
            _deploymentTracePath = Path.Combine(rootPath, Constants.DeploymentTracePath);
            _dataPath = Path.Combine(rootPath, Constants.DataPath);
            _jobsDataPath = Path.Combine(_dataPath, Constants.JobsPath);
            _jobsBinariesPath = Path.Combine(_webRootPath, Constants.AppDataPath, Constants.JobsPath);
        }

        public string RepositoryPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_repositoryPath);
            }
            set
            {
                // normalize the '/' to '\'
                _repositoryPath = Path.GetFullPath(value);
            }
        }

        public string WebRootPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_webRootPath);
            }
        }

        public string DeploymentsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_deploymentsPath);
            }
        }

        public string DeploymentToolsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_deploymentToolsPath);
            }
        }

        public string DiagnosticsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_diagnosticsPath);
            }
        }

        public string LocksPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_locksPath);
            }
        }

        public string SSHKeyPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_sshKeyPath);
            }
        }

        public string RootPath
        {
            get;
            private set;
        }

        public string SiteRootPath
        {
            get;
            private set;
        }

        public string TempPath
        {
            get
            {
                return _tempPath;
            }
        }

        public string ScriptPath
        {
            get
            {
                return _scriptPath;
            }
        }

        public string NodeModulesPath
        {
            get
            {
                return _nodeModulesPath;
            }
        }

        public string LogFilesPath
        {
            get
            {
                return _logFilesPath;
            }
        }

        public string ApplicationLogFilesPath
        {
            get
            {
                return _applicationLogFilesPath;
            }
        }

        public string TracePath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_tracePath);
            }
        }

        public string AnalyticsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_analyticsPath);
            }
        }

        public string DeploymentTracePath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_deploymentTracePath);
            }
        }

        public string DataPath
        {
            get
            {
                return _dataPath;
            }
        }

        public string JobsDataPath
        {
            get
            {
                return _jobsDataPath;
            }
        }

        public string JobsBinariesPath
        {
            get { return _jobsBinariesPath; }
        }

        public string SiteExtensionSettingsPath
        {
            get { return _siteExtensionSettingsPath; }
        }

        public static bool IsAzureEnvironment()
        {
            return !String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        }
    }
}
