using System;
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
        private readonly Func<string> _repositoryPathResolver;

        public Environment(
                IFileSystem fileSystem,
                string siteRootPath,
                string tempPath,
                Func<string> repositoryPathResolver,
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
            if (repositoryPathResolver == null)
            {
                throw new ArgumentNullException("repositoryPathResolver");
            }

            _fileSystem = fileSystem;
            SiteRootPath = siteRootPath;
            _tempPath = tempPath;
            _repositoryPathResolver = repositoryPathResolver;
            _webRootPath = webRootPath;
            _deployCachePath = deployCachePath;
            _sshKeyPath = sshKeyPath;
            NuGetCachePath = nugetCachePath;
            _scriptPath = scriptPath;
        }

        public string RepositoryPath
        {
            get
            {
                string path = _repositoryPathResolver();
                FileSystemHelpers.EnsureDirectory(_fileSystem, path);
                return path;
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
    }
}
