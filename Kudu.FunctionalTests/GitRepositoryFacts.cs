using System;
using System.IO;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Test;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    class GitRepositoryFacts
    {
        [Fact]
        public void FetchWithoutConflictOnEmptyRepoReturnsFalse()
        {
            using (TestRepository testRepository = GetRepository())
            {
                // Arrange
                var gitRepo = new GitExeRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                // Act
                gitRepo.Initialize();
                var ex = Assert.Throws<InvalidOperationException>(() => gitRepo.FetchWithoutConflict("https://github.com/KuduApps/EmptyGitRepo.git", "test", "master"));

                // Assert
                Assert.Equal("Could not fetch remote branch 'master'. Verify that the branch exists in the repository.", ex.Message);
            }
        }

        private static TestRepository GetRepository(string source = null)
        {
            source = source ?? Path.GetRandomFileName();
            string repoName = Path.GetFileNameWithoutExtension(source);
            string repoPath = Path.Combine(PathHelper.LocalRepositoriesDir, repoName);

            PathHelper.EnsureDirectory(repoPath);
            return new TestRepository(repoPath, obliterateOnDispose: true);
        }
    }
}
