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
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class GitRepositoryTest
    {
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
                repository.GitFetchWithRetry(() =>
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
