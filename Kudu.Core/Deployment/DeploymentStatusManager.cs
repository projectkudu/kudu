using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class DeploymentStatusManager : IDeploymentStatusManager
    {
        private readonly IEnvironment _environment;
        private readonly IFileSystem _fileSystem;
        private readonly string _activeFile;

        public DeploymentStatusManager(IEnvironment environment,
                                       IFileSystem fileSystem)
        {
            _environment = environment;
            _fileSystem = fileSystem;
            _activeFile = Path.Combine(environment.DeploymentCachePath, Constants.ActiveDeploymentFile);
        }

        public IDeploymentStatusFile Create(string id)
        {
            return DeploymentStatusFile.Create(id, _fileSystem, _environment);
        }

        public IDeploymentStatusFile Open(string id)
        {
            return DeploymentStatusFile.Open(id, _fileSystem, _environment);
        }

        public string ActiveDeploymentId
        {
            get
            {
                if (_fileSystem.File.Exists(_activeFile))
                {
                    return OperationManager.Attempt<string>(() => _fileSystem.File.ReadAllText(_activeFile));
                }

                return null;
            }
            set
            {
                OperationManager.Attempt(() =>
                {
                    _fileSystem.File.WriteAllText(_activeFile, value);
                });
            }
        }


        public DateTime LastModifiedTime
        {
            get
            {
                if (_fileSystem.File.Exists(_activeFile))
                {
                    return _fileSystem.File.GetLastWriteTimeUtc(_activeFile);
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
        }
    }
}
