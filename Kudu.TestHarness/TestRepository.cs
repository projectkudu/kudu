using System;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Test;
using Kudu.Core.Tracing;

namespace Kudu.TestHarness
{
    public class TestRepository : IDisposable
    {
        private readonly string _physicalPath;
        private readonly GitExeRepository _repository;
        private readonly bool _obliterateOnDispose;

        public TestRepository(string repositoryName) 
            : this(repositoryName, obliterateOnDispose: true)
        {
            
        }

        public TestRepository(string repositoryName, bool obliterateOnDispose)
        {
            _physicalPath = Git.GetRepositoryPath(repositoryName);
            _repository = new GitExeRepository(_physicalPath, null, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
            _obliterateOnDispose = obliterateOnDispose;
        }


        public string CurrentId
        {
            get
            {
                return _repository.CurrentId;
            }
        }

        public string PhysicalPath
        {
            get
            {
                return _physicalPath;
            }
        }

        public void Dispose()
        {
            if (_obliterateOnDispose)
            {
                FileSystemHelpers.DeleteDirectorySafe(PhysicalPath);
            }
        }
    }
}
