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
        private readonly string _tempPath;
        private readonly Func<string> _deploymentRepositoryPathResolver;
        private readonly Func<string> _repositoryPathResolver;

        public Environment(string applicationRootPath, 
                           string tempPath, 
                           Func<string> deploymentRepositoryPathResolver, 
                           Func<string> repositoryPathResolver, 
                           string deployPath, 
                           string deployCachePath, 
                           string nugetCachePath)
        {
            ApplicationRootPath = applicationRootPath;
            _tempPath = tempPath;
            _deploymentRepositoryPathResolver = deploymentRepositoryPathResolver;
            _repositoryPathResolver = repositoryPathResolver;
            _deployPath = deployPath;
            _deployCachePath = deployCachePath;
            NuGetCachePath = nugetCachePath;
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

                FileSystemHelpers.EnsureDirectory(path);
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
    }
}
