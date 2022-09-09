using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Helpers;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// Specific to deployment lock.
    /// </summary>
    public class DeploymentLockFile : LockFile
    {
        private string siteRoot = "";
        private bool shutdownDelayed = false;
        private readonly ITraceFactory _traceFactory;

        public DeploymentLockFile(string path, ITraceFactory traceFactory)
            : base(path, traceFactory)
        {
            OperationManager.SafeExecute(() => {
                if (Environment.IsAzureEnvironment() && OSDetector.IsOnWindows())
                {
                    siteRoot = PathUtilityFactory.Instance.ResolveLocalSitePath();
                }
            });

            _traceFactory = traceFactory;
        }

        // This is set when IRepositoryFactory is created in Ninject.
        public IRepositoryFactory RepositoryFactory
        {
            get;
            set;
        }

        protected override void OnLockAcquired()
        {
            if (ScmHostingConfigurations.FunctionsSyncTriggersDelayBackground)
            {
                shutdownDelayed = ShutdownDelaySemaphore.GetInstance().Acquire(_traceFactory.GetTracer());
            }
            else
            {
                OperationManager.SafeExecute(() => {
                    // Create Sentinel file for DWAS to check
                    // DWAS will check for presence of this file incase a an app setting based recycle needs to be performed in middle of deployment
                    // If this is present, DWAS will postpone the recycle so that deployment goes through first
                    if (!String.IsNullOrEmpty(siteRoot))
                    {
                        FileSystemHelpers.CreateDirectory(Path.Combine(siteRoot, @"ShutdownSentinel"));
                        string sentinelPath = Path.Combine(siteRoot, @"ShutdownSentinel\Sentinel.txt");

                        if (!FileSystemHelpers.FileExists(sentinelPath))
                        {
                            var file = FileSystemHelpers.CreateFile(sentinelPath);
                            file.Close();
                        }

                        // DWAS checks if write time of this file is in the future then only postpones the recycle
                        IFileInfo sentinelFileInfo = FileSystemHelpers.FileInfoFromFileName(sentinelPath);
                        sentinelFileInfo.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(20);
                    }
                });
            }

            IRepositoryFactory repositoryFactory = RepositoryFactory;
            if (repositoryFactory != null)
            {
                IRepository repository = repositoryFactory.GetRepository();
                if (repository != null)
                {
                    // Clear any left over repository-related lock since we have the actual lock
                    repository.ClearLock();
                }
            }
        }

        protected override void OnLockRelease()
        {
            base.OnLockRelease();

            if (ScmHostingConfigurations.FunctionsSyncTriggersDelayBackground && shutdownDelayed)
            {
                ShutdownDelaySemaphore.GetInstance().Release(_traceFactory.GetTracer());
            }
            else
            {
                OperationManager.SafeExecute(() => {
                    // Delete the Sentinel file to signal DWAS that deployment is complete
                    if (!String.IsNullOrEmpty(siteRoot))
                    {
                        string sentinelPath = Path.Combine(siteRoot, @"ShutdownSentinel\Sentinel.txt");

                        if (FileSystemHelpers.FileExists(sentinelPath))
                        {
                            FileSystemHelpers.DeleteFile(sentinelPath);
                        }
                    }
                });
            }
        }
    }
}