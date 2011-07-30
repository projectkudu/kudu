using Kudu.Core.Infrastructure;

namespace Kudu.Core {
    public class Environment : IEnvironment {
        private readonly string _repositoryPath;
        private readonly string _deployPath;
        private readonly string _deployCachePath;

        public Environment(string appName, string applicationRootPath, string repositoryPath, string deployPath, string deployCachePath) {
            AppName = appName;
            ApplicationRootPath = applicationRootPath;
            _repositoryPath = repositoryPath;
            _deployPath = deployPath;
            _deployCachePath = deployCachePath;
        }

        public string RepositoryPath {
            get {
                FileSystemHelpers.EnsureDirectory(_repositoryPath);
                return _repositoryPath;
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
