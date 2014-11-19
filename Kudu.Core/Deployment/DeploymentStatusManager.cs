using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentStatusManager : IDeploymentStatusManager
    {
        public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(60);
        private readonly IEnvironment _environment;
        private readonly IAnalytics _analytics;
        private readonly IOperationLock _statusLock;
        private readonly string _activeFile;

        public DeploymentStatusManager(IEnvironment environment,
                                       IAnalytics analytics,
                                       IOperationLock statusLock)
        {
            _environment = environment;
            _analytics = analytics;
            _statusLock = statusLock;
            _activeFile = Path.Combine(environment.DeploymentsPath, Constants.ActiveDeploymentFile);
        }

        public IDeploymentStatusFile Create(string id)
        {
            return DeploymentStatusFile.Create(id, _environment, _statusLock);
        }

        public IDeploymentStatusFile Open(string id)
        {
            return DeploymentStatusFile.Open(id, _environment, _analytics, _statusLock);
        }

        public void Delete(string id)
        {
            string path = Path.Combine(_environment.DeploymentsPath, id);

            _statusLock.LockOperation(() =>
            {
                FileSystemHelpers.DeleteDirectorySafe(path, ignoreErrors: true);

                // Used for ETAG
                if (FileSystemHelpers.FileExists(_activeFile))
                {
                    FileSystemHelpers.SetLastWriteTimeUtc(_activeFile, DateTime.UtcNow);
                }
                else
                {
                    FileSystemHelpers.WriteAllText(_activeFile, String.Empty);
                }
            }, LockTimeout);
        }

        public IOperationLock Lock
        {
            get { return _statusLock; }
        }

        public string ActiveDeploymentId
        {
            get
            {
                return _statusLock.LockOperation(() =>
                {
                    if (FileSystemHelpers.FileExists(_activeFile))
                    {
                        return FileSystemHelpers.ReadAllText(_activeFile);
                    }

                    return null;
                }, LockTimeout);
            }
            set
            {
                _statusLock.LockOperation(() => FileSystemHelpers.WriteAllText(_activeFile, value), LockTimeout);
            }
        }


        public DateTime LastModifiedTime
        {
            get
            {
                return _statusLock.LockOperation(() =>
                {
                    if (FileSystemHelpers.FileExists(_activeFile))
                    {
                        return FileSystemHelpers.GetLastWriteTimeUtc(_activeFile);
                    }
                    else
                    {
                        return DateTime.MinValue;
                    }
                }, LockTimeout);
            }
        }
    }
}
