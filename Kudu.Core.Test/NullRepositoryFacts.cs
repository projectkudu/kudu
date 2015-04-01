using System;
using System.Collections;
using System.IO;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class NullRepositoryFacts
    {
        [Theory]
        [InlineData("wwwroot", null, "1")]
        [InlineData("repository", null, "0")]
        [InlineData("dummy", "dummy", "1")]
        [InlineData("dummy", "dummy", null)]
        public void NullRepositoryPathTests(string expected, string repositoryPath, string noRepository)
        {
            // arrange
            var settings = MockSettings(repositoryPath, noRepository);

            // Assert
            Assert.Equal(expected, settings.Object.GetRepositoryPath());
        }

        [Fact]
        public void NullRepositoryCommitTests()
        {
            // arrange
            var settings = MockSettings();
            var environment = MockEnviroment(@"x:\site", settings.Object);
            var traceFactory = MockTraceFactory();
            var httpContext = MockHttpContext();

            // test
            var repository = new NullRepository(environment.Object, traceFactory.Object, httpContext.Object);

            // Assert
            Assert.Equal(RepositoryType.None, repository.RepositoryType);
            Assert.Equal(environment.Object.RepositoryPath, repository.RepositoryPath);
            Assert.Null(repository.CurrentId);
            Assert.Throws<InvalidOperationException>(() => repository.GetChangeSet("dummy"));
            Assert.Throws<InvalidOperationException>(() => repository.GetChangeSet("master"));

            // Simple commit
            var message = "this is testing";
            var author = "john doe";
            var email = "john.doe@live.com";
            var success = repository.Commit(message, author, email);

            // Assert
            Assert.True(success);
            Assert.NotNull(repository.CurrentId);

            // Validate changeset
            var changeSet = repository.GetChangeSet(repository.CurrentId);

            // Assert
            Assert.NotNull(changeSet);
            Assert.Equal(message, changeSet.Message);
            Assert.Equal(author, changeSet.AuthorName);
            Assert.Equal(email, changeSet.AuthorEmail);
            Assert.True(changeSet.IsReadOnly);
            Assert.Equal(repository.CurrentId, changeSet.Id);
            Assert.Throws<InvalidOperationException>(() => repository.GetChangeSet("dummy"));
            Assert.Same(changeSet, repository.GetChangeSet("master"));
            Assert.Same(changeSet, repository.GetChangeSet(repository.CurrentId));
        }

        private Mock<IDeploymentSettingsManager> MockSettings(string repositoryPath = null, string noRepository = "1")
        {
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);

            // setup
            settings.Setup(s => s.GetValue(SettingsKeys.RepositoryPath, false))
                    .Returns(repositoryPath);
            settings.Setup(s => s.GetValue(SettingsKeys.NoRepository, false))
                    .Returns(noRepository);

            return settings;
        }

        private Mock<IEnvironment> MockEnviroment(string sitePath, IDeploymentSettingsManager settings)
        {
            var environment = new Mock<IEnvironment>(MockBehavior.Strict);

            // setup
            environment.SetupGet(e => e.RepositoryPath)
                      .Returns(() => Path.Combine(sitePath, settings.GetRepositoryPath()));

            return environment;
        }

        private Mock<ITraceFactory> MockTraceFactory()
        {
            var traceFactory = new Mock<ITraceFactory>(MockBehavior.Strict);

            // setup
            traceFactory.Setup(t => t.GetTracer())
                        .Returns(Mock.Of<ITracer>());

            return traceFactory;
        }

        private Mock<HttpContextBase> MockHttpContext()
        {
            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);

            // setup
            httpContext.SetupGet(c => c.Items)
                       .Returns(new Hashtable());

            return httpContext;
        }
    }
}