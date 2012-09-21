using System;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using System.IO;

namespace Kudu.Core
{
    public class Environment : IEnvironment
    {
        private readonly string _deployPath;
        private readonly string _deployCachePath;
        private readonly string _sshKeyPath;
        private readonly string _tempPath;
        private readonly string _scriptPath;
        private readonly Func<string> _deploymentRepositoryPathResolver;
        private readonly Func<string> _repositoryPathResolver;

        public Environment(string applicationRootPath, 
                           string tempPath, 
                           Func<string> deploymentRepositoryPathResolver, 
                           Func<string> repositoryPathResolver, 
                           string deployPath, 
                           string deployCachePath,
                           string sshKeyPath, 
                           string nugetCachePath, 
                           string scriptPath)
        {
            ApplicationRootPath = applicationRootPath;
            _tempPath = tempPath;
            _deploymentRepositoryPathResolver = deploymentRepositoryPathResolver;
            _repositoryPathResolver = repositoryPathResolver;
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
                FileSystemHelpers.EnsureDirectory(path);
                return path;
            }
        }

        public string RepositoryPath
        {
            get
            {
                string path = _repositoryPathResolver();
                if (String.IsNullOrEmpty(path))
                {
                    return null;
                }

                return path;
            }
        }

        public string DeploymentTargetPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_deployPath);
                return _deployPath;
            }
        }

        public string DeploymentCachePath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_deployCachePath);
                return _deployCachePath;
            }
        }

        public string SSHKeyPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_sshKeyPath);
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
