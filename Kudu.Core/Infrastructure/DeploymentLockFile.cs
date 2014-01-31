using System;
using System.IO.Abstractions;
using Kudu.Contracts.SourceControl;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// Specific to deployment lock.
    /// </summary>
    public class DeploymentLockFile : LockFile
    {
        public DeploymentLockFile(string path, ITraceFactory traceFactory)
            : base(path, traceFactory)
        {
        }

        // This is set when IRepositoryFactory is created in Ninject.
        public IRepositoryFactory RepositoryFactory
        {
            get;
            set;
        }

        protected override void OnLockAcquired()
        {
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
    }
}