using System;
using Kudu.Core;
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
        private readonly IEnvironment _environment;
        private readonly bool _obliterateOnDispose;

        public TestRepository(string repositoryName) 
            : this(repositoryName, obliterateOnDispose: true)
        {
            
        }

        public TestRepository(string repositoryName, bool obliterateOnDispose)
        {
            _physicalPath = Git.GetRepositoryPath(repositoryName);
            _environment = new TestEnvironment { RepositoryPath = _physicalPath };
            _repository = new GitExeRepository(_environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
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

        public IEnvironment Environment
        {
            get
            {
                return _environment;
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
