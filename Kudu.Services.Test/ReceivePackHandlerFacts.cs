using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.GitServer;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class ReceivePackHandlerFacts
    {
        [Fact]
        public void ReceivePackHandlerThrowsIfPushingAGitRepositoryWhenHgRepositoryIsAlreadyPresent()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType", false)).Returns("Git");

            var response = Mock.Of<HttpResponseBase>();
            var context = new Mock<HttpContextBase>();
            context.SetupGet(c => c.Response).Returns(response);

            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            var repository = new Mock<IRepository>();
            repository.SetupGet(r => r.RepositoryType).Returns(RepositoryType.Mercurial);
            repositoryFactory.Setup(s => s.GetRepository()).Returns(repository.Object);

            ReceivePackHandler handler = CreateHandler(settings: settings.Object, repositoryFactory: repositoryFactory.Object);

            // Act
            handler.ProcessRequestBase(context.Object);

            // Assert
            Assert.Equal(400, response.StatusCode);
        }

        private ReceivePackHandler CreateHandler(IGitServer gitServer = null, 
                                                IDeploymentManager deploymentManager = null, 
                                                IDeploymentSettingsManager settings = null, 
                                                IRepositoryFactory repositoryFactory = null,
                                                IAutoSwapHandler autoSwapHandler = null)
        {
            return new ReceivePackHandler(Mock.Of<ITracer>(),
                                          gitServer ?? Mock.Of<IGitServer>(),
                                          Mock.Of<IOperationLock>(),
                                          deploymentManager ?? Mock.Of<IDeploymentManager>(),
                                          repositoryFactory ?? Mock.Of<IRepositoryFactory>(),
                                          autoSwapHandler ?? Mock.Of<IAutoSwapHandler>());
        }
    }
}
