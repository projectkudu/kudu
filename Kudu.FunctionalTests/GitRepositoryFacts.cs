using System;
using System.IO;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Test;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class GitRepositoryFacts
    {
        [Fact]
        public void GitInitializeCreatesPostCommitHook()
        {
            using (TestRepository testRepository = GetRepository())
            {
                // Arrange
                var gitRepo = new GitExeRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                string postCommitHookPath = Path.Combine(testRepository.PhysicalPath, ".git", "hooks", "post-receive");
                string expected = "#!/bin/sh\r\nread i\r\necho $i > pushinfo\r\n\"$KUDU_EXE\" \"$KUDU_APPPATH\" \"$KUDU_MSBUILD\" \"$KUDU_DEPLOYER\"\n";

                // Act
                gitRepo.Initialize();

                // Assert
                Assert.Equal(expected, File.ReadAllText(postCommitHookPath));
            }
        }

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
