using System;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;

namespace Kudu.TestHarness
{
    public class TestRepository : IDisposable
    {
        private readonly string _physicalPath;
        private readonly GitExeRepository _repository;

        public TestRepository(string repositoryName)
        {
            _physicalPath = Git.GetRepositoryPath(repositoryName);
            _repository = new GitExeRepository(_physicalPath);
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
            FileSystemHelpers.DeleteDirectorySafe(PhysicalPath);
        }
    }
}
