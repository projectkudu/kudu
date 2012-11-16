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
        private readonly string _deployCachePath;
        private readonly string _sshKeyPath;
        private readonly string _tempPath;
        private readonly string _scriptPath;
        private readonly string _repositoryPath;
        private readonly string _tracePath;
        private readonly string _deploymentTracePath;

        public Environment(
                IFileSystem fileSystem,
                string rootPath,
                string siteRootPath,
                string tempPath,
                string repositoryPath,
                string webRootPath,
                string deployCachePath,
                string sshKeyPath,
                string nugetCachePath,
                string scriptPath)
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
            _deployCachePath = deployCachePath;
            _sshKeyPath = sshKeyPath;
            NuGetCachePath = nugetCachePath;
            _scriptPath = scriptPath;
            _tracePath = Path.Combine(rootPath, Constants.TracePath);
            _deploymentTracePath = Path.Combine(rootPath, Constants.DeploymentTracePath);
        }

        public string RepositoryPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _repositoryPath);
                return _repositoryPath;
            }
        }

        public string WebRootPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _webRootPath);
                return _webRootPath;
            }
        }

        public string DeploymentCachePath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _deployCachePath);
                return _deployCachePath;
            }
        }

        public string SSHKeyPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _sshKeyPath);
                return _sshKeyPath;
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

        public string NuGetCachePath
        {
            get;
            private set;
        }

        public string ScriptPath
        {
            get
            {
                return _scriptPath;
            }
        }

        public string TracePath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _tracePath);
                return _tracePath;
            }
        }

        public string DeploymentTracePath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _deploymentTracePath);
                return _deploymentTracePath;
            }
        }
    }
}
