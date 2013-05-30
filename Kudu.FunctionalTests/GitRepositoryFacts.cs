using System;
using System.Collections.Generic;
using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Test;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Xunit;
using Xunit.Extensions;

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
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                string postCommitHookPath = Path.Combine(testRepository.PhysicalPath, ".git", "hooks", "post-receive");
                string expected = "#!/bin/sh\r\nread i\r\necho $i > pushinfo\r\n\"$KUDU_EXE\" \"$KUDU_APPPATH\" \"$KUDU_MSBUILD\" \"$KUDU_DEPLOYER\"\n";

                // Act
                gitRepo.Initialize();

                // Assert
                Assert.Equal(expected, File.ReadAllText(postCommitHookPath));
            }
        }

        [Fact]
        public void FetchWithoutConflictOnGitEmptyRepo()
        {
            using (TestRepository testRepository = GetRepository())
            {
                // Arrange
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                // Act
                gitRepo.Initialize();
                Assert.Throws<BranchNotFoundException>(() => gitRepo.FetchWithoutConflict("https://github.com/KuduApps/EmptyGitRepo.git", "master"));
            }
        }

        [Fact]
        public void GitRepoDoesntExistBeforeInitialize()
        {
            using (TestRepository testRepository = GetRepository())
            {
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                Assert.False(gitRepo.Exists, "git repository shouldn't exist yet");
            }
        }

        [Fact]
        public void GitRepoExistsAfterInitialize()
        {
            using (TestRepository testRepository = GetRepository())
            {
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                gitRepo.Initialize();
                Assert.True(gitRepo.Exists, "git repository should exist");
            }
        }

        [Fact]
        public void GitRepoDoesntExistIfCorrupted()
        {
            using (TestRepository testRepository = GetRepository())
            {
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                gitRepo.Initialize();
                Assert.True(gitRepo.Exists, "git repository should exist");

                string gitHeadPath = Path.Combine(testRepository.PhysicalPath, ".git", "HEAD");
                File.Delete(gitHeadPath);
                Assert.False(gitRepo.Exists, "git repository shouldn't exist");
            }
        }

        [Fact]
        public void GitRepoExistIfCorruptedThenInitializedAgain()
        {
            using (TestRepository testRepository = GetRepository())
            {
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                gitRepo.Initialize();
                Assert.True(gitRepo.Exists, "git repository should exist");

                string gitHeadPath = Path.Combine(testRepository.PhysicalPath, ".git", "HEAD");
                File.Delete(gitHeadPath);
                Assert.False(gitRepo.Exists, "git repository shouldn't exist");

                gitRepo.Initialize();
                Assert.True(gitRepo.Exists, "git repository should exist");
            }
        }

        [Fact]
        public void GitRepoDoesntExistIfGitRepoOnlyOnParentDirectory()
        {
            using (TestRepository testRepository = GetRepository())
            {
                // Create a repository
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                gitRepo.Initialize();

                // Checkout for existence in subdirectory
                var testedPath = Path.Combine(testRepository.PhysicalPath, "subdirectory");
                Directory.CreateDirectory(testedPath);
                var environment = new TestEnvironment { RepositoryPath = testedPath };
                gitRepo = new GitExeRepository(environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                Assert.False(gitRepo.Exists, "git repository shouldn't exist yet");
            }
        }

        [Theory]
        [PropertyData("ParseCommitData")]
        public void GitRepoParsesCommitDetails(string id, ChangeSet expectedChangeset)
        {
            using (var testRepository = Git.Clone("Mvc3Application_NoSolution"))
            {
                // Arrange
                var gitRepo = new GitExeRepository(testRepository.Environment, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                // Act
                var changeset = gitRepo.GetChangeSet(id);

                // Assert
                Assert.Equal(expectedChangeset.Id, changeset.Id);
                Assert.Equal(expectedChangeset.AuthorName, changeset.AuthorName);
                Assert.Equal(expectedChangeset.AuthorEmail, changeset.AuthorEmail);
                Assert.Equal(expectedChangeset.Message, changeset.Message.Trim());
                Assert.Equal(expectedChangeset.Timestamp, changeset.Timestamp);
            }
        }

        public static IEnumerable<object[]> ParseCommitData
        {
            get
            {
                yield return new object[] { "HEAD", new ChangeSet("4e36ca31aa30ea08a5e5d38c65652a020d48e1d0", "Raquel Almeida", "raquel_soares@msn.com", "Chaning Index view", new DateTimeOffset(2011, 11, 23, 10, 30, 16, TimeSpan.FromHours(-8))) };
                yield return new object[] { "89d70221f6a86d4243af3df7a8c80e65a29429af", new ChangeSet("89d70221f6a86d4243af3df7a8c80e65a29429af", "Raquel Almeida", "raquel_soares@msn.com", "Initial commit", new DateTimeOffset(2011, 11, 23, 10, 02, 34, TimeSpan.FromHours(-8))) };
            }
        }

        [Fact]
        public void GitClearLockRemovesHeadAndIndexLocks()
        {
            using (var testRepo = GetRepository())
            {
                // Arrange
                Git.Init(testRepo.PhysicalPath);
                string fileToWrite = Path.Combine(testRepo.PhysicalPath, "some file.txt");
                File.WriteAllText(Path.Combine(testRepo.PhysicalPath, ".git", "index.lock"), "");
                File.WriteAllText(Path.Combine(testRepo.PhysicalPath, ".git", "HEAD.lock"), "");
                File.WriteAllText(fileToWrite, "Hello world");
                var env = new TestEnvironment
                {
                    RepositoryPath = testRepo.PhysicalPath
                };
                var gitRepo = new GitExeRepository(env, new MockDeploymentSettingsManager(), NullTracerFactory.Instance);


                // Assert - 1
                var ex = Assert.Throws<CommandLineException>(() => Git.Add(testRepo.PhysicalPath, fileToWrite));
                Assert.Contains(".git/index.lock': File exists.", ex.Message);
                
                // Act - 2
                gitRepo.ClearLock();
                Git.Add(testRepo.PhysicalPath, fileToWrite);
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

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
