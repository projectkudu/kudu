using System;
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
        public void ReceivePackHandlerReturns403IfScmIsNotAvailable()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType")).Returns("None");

            var response = Mock.Of<HttpResponseBase>();
            var context = new Mock<HttpContextBase>();
            context.SetupGet(c => c.Response).Returns(response);

            ReceivePackHandler handler = CreateHandler(settings: settings.Object);

            // Act
            handler.ProcessRequestBase(context.Object);

            // Assert
            Assert.Equal(403, response.StatusCode);
        }

        [Fact]
        public void ReceivePackHandlerThrowsIfPushingAGitRepositoryWhenHgRepositoryIsAlreadyPresent()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType")).Returns("Git");

            var exception = new Exception();
            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            repositoryFactory.Setup(s => s.EnsureRepository(RepositoryType.Git)).Throws(exception);

            ReceivePackHandler handler = CreateHandler(settings: settings.Object, repositoryFactory: repositoryFactory.Object);

            // Act and Assert
            var ex = Assert.Throws<Exception>(() => handler.ProcessRequestBase(Mock.Of<HttpContextBase>()));

            // Assert
            Assert.Equal(exception, ex);
        }

        private ReceivePackHandler CreateHandler(IGitServer gitServer = null, 
                                                IDeploymentManager deploymentManager = null, 
                                                IDeploymentSettingsManager settings = null, 
                                                IRepositoryFactory repositoryFactory = null)
        {
            return new ReceivePackHandler(Mock.Of<ITracer>(),
                                          gitServer ?? Mock.Of<IGitServer>(),
                                          Mock.Of<IOperationLock>(),
                                          deploymentManager ?? Mock.Of<IDeploymentManager>(),
                                          settings ?? Mock.Of<IDeploymentSettingsManager>(),
                                          repositoryFactory ?? Mock.Of<IRepositoryFactory>());
        }
    }
}
