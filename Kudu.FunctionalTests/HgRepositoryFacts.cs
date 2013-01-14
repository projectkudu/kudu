using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.SourceControl;
using Kudu.Core.Test;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    public class HgRepositoryFacts
    {
        [Fact]
        public void HgExecutableClonesRepository()
        {
            const string expectedId = "42c0d799763d7acbe4312d000f771ec0afa0d6ab";
            const string source = "https://kudutest@bitbucket.org/kudutest/hellomercurial";
            // Arrange
            using (TestRepository testRepository = GetRepository(source))
            {
                string helloTextPath = Path.Combine(testRepository.PhysicalPath, "Hello.txt");
                string hgFolderPath = Path.Combine(testRepository.PhysicalPath, ".hg");
                var hgRepo = new HgRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                // Act
                hgRepo.Clone(source);
                string actualId = hgRepo.CurrentId;

                // Assert
                Assert.True(File.Exists(helloTextPath));
                Assert.True(Directory.Exists(hgFolderPath));
                Assert.Equal(expectedId, actualId);
            }
        }

        [Fact]
        public void HgRepositoryCanFetchBranchFromRemoteRepository()
        {
            const string repositoryName = "fetchTest";
            using (TestRepository testRepository = GetRepository(repositoryName))
            {
                // Arrange
                string remoteRepository = "https://kudutest@bitbucket.org/kudutest/hellomercurial";
                string helloTextPath = Path.Combine(testRepository.PhysicalPath, "Hello.txt");
                var hgRepo = new HgRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                hgRepo.Initialize();

                // Act - 1
                hgRepo.FetchWithoutConflict(remoteRepository, remoteAlias: null, branchName: "default");
                
                // Assert - 1
                Assert.Equal("Hello mercurial", File.ReadAllText(helloTextPath));
                Assert.Equal("42c0d799763d7acbe4312d000f771ec0afa0d6ab", hgRepo.CurrentId);

                // Act - 2
                // Make uncommitted changes
                File.WriteAllText(helloTextPath, "uncommitted changes");

                // Act - 2
                hgRepo.FetchWithoutConflict(remoteRepository, remoteAlias: null, branchName: "test");

                // Assert - 2
                Assert.Equal("This is a commit from test", File.ReadAllText(helloTextPath));
                Assert.Equal("7648ca7e03987b5d4204fcb283c687dada051ce5", hgRepo.CurrentId);
            }
        }

        [Fact]
        public void ChangeLogFromHgRepositoryAreAccurate()
        {
            const string repositoryName = "changeLog";
            using (TestRepository testRepository = GetRepository(repositoryName))
            {
                // Arrange
                string helloTextPath = Path.Combine(testRepository.PhysicalPath, "Hello.txt");
                var hgRepo = new HgRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                // Act
                hgRepo.Initialize();
                File.WriteAllText(helloTextPath, "Hello world");
                hgRepo.AddFile(helloTextPath);
                hgRepo.Commit("First commit");
                File.AppendAllText(helloTextPath, "Hello again");
                hgRepo.Commit("Second commit");
                List<ChangeSet> changes = hgRepo.GetChanges().ToList();

                // Assert - 1
                Assert.Equal(2, changes.Count());
                var lastChange = changes[0];
                
                Assert.Equal("Second commit", lastChange.Message);
            }
        }

        private static TestRepository GetRepository(string source)
        {
            string repoName = KuduUtils.GetRandomWebsiteName(Path.GetFileNameWithoutExtension(source));
            string repoPath = Path.Combine(PathHelper.LocalRepositoriesDir, repoName);

            PathHelper.EnsureDirectory(repoPath);
            return new TestRepository(repoPath, obliterateOnDispose: true);
        }
    }
}
