using System;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class GitRepositoryTest
    {
        [Fact]
        public void ParseCommitParsesCommit()
        {
            string commitText = @"commit 307d8fe354ff30609decef49f91195e2e9719398
Author: David Fowler <davidfowl@gmail.com>
Date:   Thu Jul 7 19:05:40 2011 -0700

    Initial commit";

            ChangeSet changeSet = GitExeRepository.ParseCommit(commitText.AsReader());

            Assert.Equal("307d8fe354ff30609decef49f91195e2e9719398", changeSet.Id);
            Assert.Equal("David Fowler", changeSet.AuthorName);
            Assert.Equal("davidfowl@gmail.com", changeSet.AuthorEmail);
            Assert.Equal("Initial commit", changeSet.Message);
        }

        [Fact]
        public void ParseCommitWithMultipleCommitsParsesOneCommit()
        {
            string commitText = @"commit d35697645e2472f5e327c0ec4b9f3489e806c276
Author: John Doe
Date:   Thu Jul 7 19:23:07 2011 -0700

    Second commit

commit 307d8fe354ff30609decef49f91195e2e9719398
Author: David Fowler <davidfowl@gmail.com>
Date:   Thu Jul 7 19:05:40 2011 -0700

    Initial commit
";

            ChangeSet changeSet = GitExeRepository.ParseCommit(commitText.AsReader());

            Assert.Equal("d35697645e2472f5e327c0ec4b9f3489e806c276", changeSet.Id);
            Assert.Equal("John Doe", changeSet.AuthorName);
            Assert.Null(changeSet.AuthorEmail);
            Assert.Equal(@"Second commit", changeSet.Message);
        }

        [Theory]
        [InlineData(" a b c \\d e f", " a b c \\d e f")]
        [InlineData("\"\\303\\245benr\\303\\245.sln\"", "\"åbenrå.sln\"")]
        [InlineData("\"\\303\\245benr\\303\\245/\\303\\245benr\\303\\245.csproj\"", "\"åbenrå/åbenrå.csproj\"")]
        public void DecodeGitExeOutputToUtf8(string original, string expected)
        {
            Assert.Equal(GitExeRepository.DecodeGitLsOutput(original), expected);
        }

        [Theory]
        [InlineData(null, 1)]
        [InlineData("This is non-retryable exception", 1)]
        [InlineData("Unknown SSL protocol error in connection to github.com:443", 3)]
        [InlineData("error: The requested URL returned error: 403 while accessing https://github.com/KuduApps/EmptyGitRepo.git/info/refs", 3)]
        [InlineData("fatal: HTTP request failed", 3)]
        [InlineData("fatal: The remote end hung up unexpectedly", 3)]
        public void GitExecuteWithRetryTest(string message, int expect)
        {
            // Mock
            var settings = new Mock<IDeploymentSettingsManager>();
            var trace = new Mock<ITraceFactory>();

            var repository = new GitExeRepository(Mock.Of<IEnvironment>(), settings.Object, trace.Object);
            Exception exception = null;
            var actual = 0;

            // Setup
            trace.Setup(t => t.GetTracer())
                 .Returns(() => NullTracer.Instance);

            // Test
            try
            {
                repository.ExecuteGenericGitCommandWithRetryAndCatchingWellKnownGitErrors(() =>
                {
                    ++actual;
                    if (message == null)
                    {
                        return true;
                    }

                    throw new Exception(message);
                });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            Assert.Equal(expect, actual);
            Assert.Equal(message, (exception == null) ? null : exception.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RepositoryConcurrentInitialize(bool initialized)
        {
            // Mock
            var initLock = new OperationLockTests.MockOperationLock();
            var factory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            var env = new Mock<IDeploymentEnvironment>();
            var settings = new Mock<IDeploymentSettingsManager>();
            var trace = new Mock<ITraceFactory>();

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.RepositoryPath)
                       .Returns(String.Empty);
            environment.SetupGet(e => e.SiteRootPath)
                       .Returns(String.Empty);
            environment.SetupGet(e => e.RootPath)
                       .Returns(String.Empty);

            IRepository repository = initialized ? new Mock<IRepository>().Object : null;
            var server = new GitExeServer(environment.Object, initLock, null, factory.Object, env.Object, settings.Object, trace.Object);
            var calls = 0;

            // Setup
            trace.Setup(t => t.GetTracer())
                 .Returns(() => NullTracer.Instance);
            factory.Setup(f => f.GetRepository())
                   .Returns(() => repository);
            factory.Setup(f => f.EnsureRepository(RepositoryType.Git))
                   .Returns(() => 
                    {
                        ++calls;
                        Thread.Sleep(100);
                        Assert.Null(repository);
                        return repository = new Mock<IRepository>().Object;
                    });

            // Test
            Parallel.For(0, 5, i => server.Initialize());

            // Assert
            Assert.NotNull(repository);
            Assert.Equal(initialized ? 0 : 1, calls);
        }
    }
}
