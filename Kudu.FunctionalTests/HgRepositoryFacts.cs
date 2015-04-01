using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Test;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class HgRepositoryFacts
    {
        [Fact]
        public void HgGetChangeSetReturnsNullIfIdDoesNotExist()
        {
            // Arrange
            using (TestRepository testRepository = GetRepository())
            {
                var hgRepo = new HgRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);
                hgRepo.Initialize();

                // Act
                var changeset = hgRepo.GetChangeSet("does-not-exist");

                // Assert
                Assert.Null(changeset);
            }
        }

        [Fact]
        public void HgExecutableClonesRepository()
        {
            const string expectedId = "e2ff43634d31a70383142a4b3940baff8b6386ee";
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
                hgRepo.FetchWithoutConflict(remoteRepository, branchName: "default");
                
                // Assert - 1
                Assert.Equal("Hello mercurial!", File.ReadAllText(helloTextPath));
                Assert.Equal("e2ff43634d31a70383142a4b3940baff8b6386ee", hgRepo.CurrentId);

                // Act - 2
                // Make uncommitted changes
                File.WriteAllText(helloTextPath, "uncommitted changes");

                // Act - 2
                hgRepo.FetchWithoutConflict(remoteRepository, branchName: "test");

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

        [Fact]
        public void FetchWithoutConflictOnEmptyRepoReturnsFalse()
        {
            using (TestRepository testRepository = GetRepository())
            {
                // Arrange
                var hgRepo = new HgRepository(testRepository.PhysicalPath, "", new MockDeploymentSettingsManager(), NullTracerFactory.Instance);

                // Act
                hgRepo.Initialize();
                Assert.Throws<BranchNotFoundException>(() => hgRepo.FetchWithoutConflict("https://bitbucket.org/kudutest/emptyhgrepo", "default"));
            }
        }

        [Fact]
        public void FetchWithoutConflictMessageMatchesEmbeddedErrorString()
        {
            // This test verifies if the embedded string matches the exception message mercurial throws.
            using (TestRepository testRepository = CreateRecoveryRepo())
            {
                // Arrange
                var executable = HgRepository.GetHgExecutable(testRepository.PhysicalPath, TimeSpan.FromMinutes(1));

                // Act
                var ex = Assert.Throws<CommandLineException>(() => executable.Execute(NullTracer.Instance, "pull https://bitbucket.org/kudutest/hellomercurial"));

                // Assert
                Assert.Contains("abort: abandoned transaction found", ex.Message);
                
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

        private static TestRepository CreateRecoveryRepo()
        {
            var repository = GetRepository();
            string hgPath = Path.Combine(repository.PhysicalPath, ".hg");

            WriteManifestFile(@"Kudu.FunctionalTests.Test_Files.Hg.requires", Path.Combine(hgPath, "requires"));
            WriteManifestFile(@"Kudu.FunctionalTests.Test_Files.Hg.journal", Path.Combine(hgPath, "store", "journal"));

            return repository;
        }

        private static void WriteManifestFile(string manifestResourceName, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var assembly =  typeof(HgRepositoryFacts).Assembly;
            using (Stream outStream = File.Open(path, FileMode.Create, FileAccess.Write),
                          inStream  = assembly.GetManifestResourceStream(manifestResourceName))
            {
                inStream.CopyTo(outStream);
            }
        }
    }
}
