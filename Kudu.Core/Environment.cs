using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;

namespace Kudu.Core
{
    public class Environment : IEnvironment
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _webRootPath;
        private readonly string _deploymentsPath;
        private readonly string _deploymentToolsPath;
        private readonly string _diagnosticsPath;
        private readonly string _sshKeyPath;
        private readonly string _tempPath;
        private readonly string _scriptPath;
        private readonly string _nodeModulesPath;
        private readonly string _repositoryPath;
        private readonly string _logFilesPath;
        private readonly string _tracePath;
        private readonly string _deploymentTracePath;

        public Environment(
                IFileSystem fileSystem,
                string rootPath,
                string siteRootPath,
                string tempPath,
                string repositoryPath,
                string webRootPath,
                string deploymentsPath,
                string diagnosticsPath,
                string sshKeyPath,
                string scriptPath,
                string nodeModulesPath)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }
            if (repositoryPath == null)
            {
                throw new ArgumentNullException("repositoryPath");
            }

            _fileSystem = fileSystem;
            RootPath = rootPath;
            SiteRootPath = siteRootPath;
            _tempPath = tempPath;
            _repositoryPath = repositoryPath;
            _webRootPath = webRootPath;
            _deploymentsPath = deploymentsPath;
            _deploymentToolsPath = Path.Combine(_deploymentsPath, Constants.DeploymentToolsPath);
            _diagnosticsPath = diagnosticsPath;
            _sshKeyPath = sshKeyPath;
            _scriptPath = scriptPath;
            _nodeModulesPath = nodeModulesPath;
            _logFilesPath = Path.Combine(rootPath, Constants.LogFilesPath);
            _tracePath = Path.Combine(rootPath, Constants.TracePath);
            _deploymentTracePath = Path.Combine(rootPath, Constants.DeploymentTracePath);
        }

        public string RepositoryPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _repositoryPath);
            }
        }

        public string WebRootPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _webRootPath);
            }
        }

        public string DeploymentsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _deploymentsPath);
            }
        }

        public string DeploymentToolsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _deploymentToolsPath);
            }
        }

        public string DiagnosticsPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _diagnosticsPath);
            }
        }

        public string SSHKeyPath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _sshKeyPath);
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

        public string TracePath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _tracePath);
            }
        }

        public string DeploymentTracePath
        {
            get
            {
                return FileSystemHelpers.EnsureDirectory(_fileSystem, _deploymentTracePath);
            }
        }
    }
}
