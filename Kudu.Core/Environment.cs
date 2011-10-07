using Kudu.Core.Infrastructure;
using System;

namespace Kudu.Core {
    public class Environment : IEnvironment {
        private readonly string _deployPath;
        private readonly string _deployCachePath;
        private readonly string _deploymentRepositoryPath;
        private readonly Func<string> _repositoryPathResolver;

        public Environment(string appName,
                           string applicationRootPath,
                           string deploymentRepositoryPath,
                           Func<string> repositoryPathResolver,
                           string deployPath,
                           string deployCachePath) {
            AppName = appName;
            ApplicationRootPath = applicationRootPath;
            _deploymentRepositoryPath = deploymentRepositoryPath;
            _repositoryPathResolver = repositoryPathResolver;
            _deployPath = deployPath;
            _deployCachePath = deployCachePath;
        }

        public string DeploymentRepositoryPath {
            get {
                FileSystemHelpers.EnsureDirectory(_deploymentRepositoryPath);
                return _deploymentRepositoryPath;
            }
        }

        public string RepositoryPath {
            get {
                string path = _repositoryPathResolver();
                if (String.IsNullOrEmpty(path)) {
                    return null;
                }

                FileSystemHelpers.EnsureDirectory(path);
                return path;
            }
        }

        public string DeploymentTargetPath {
            get {
                FileSystemHelpers.EnsureDirectory(_deployPath);
                return _deployPath;
            }
        }

        public string DeploymentCachePath {
            get {
                FileSystemHelpers.EnsureDirectory(_deployCachePath);
                return _deployCachePath;
            }
        }

        public string ApplicationRootPath {
            get;
            private set;
        }

        public string AppName {
            get;
            private set;
        }
    }
}
