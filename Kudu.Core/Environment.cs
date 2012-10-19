using System;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;

namespace Kudu.Core
{
    public class Environment : IEnvironment
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _deployPath;
        private readonly string _deployCachePath;
        private readonly string _sshKeyPath;
        private readonly string _tempPath;
        private readonly string _scriptPath;
        private readonly Func<string> _deploymentRepositoryPathResolver;

        public Environment(
                IFileSystem fileSystem,
                string applicationRootPath,
                string tempPath,
                Func<string> deploymentRepositoryPathResolver,
                string deployPath,
                string deployCachePath,
                string sshKeyPath,
                string nugetCachePath,
                string scriptPath)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }
            if (deploymentRepositoryPathResolver == null)
            {
                throw new ArgumentNullException("deploymentRepositoryPathResolver");
            }

            _fileSystem = fileSystem;
            ApplicationRootPath = applicationRootPath;
            _tempPath = tempPath;
            _deploymentRepositoryPathResolver = deploymentRepositoryPathResolver;
            _deployPath = deployPath;
            _deployCachePath = deployCachePath;
            _sshKeyPath = sshKeyPath;
            NuGetCachePath = nugetCachePath;
            _scriptPath = scriptPath;
        }

        public string DeploymentRepositoryPath
        {
            get
            {
                string path = _deploymentRepositoryPathResolver();
                FileSystemHelpers.EnsureDirectory(_fileSystem, path);
                return path;
            }
        }

        public string DeploymentTargetPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _deployPath);
                return _deployPath;
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

        public string ApplicationRootPath
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
    }
}
