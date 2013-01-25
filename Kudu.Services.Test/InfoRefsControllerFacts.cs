using System;
using System.Net;
using System.Net.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.GitServer;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class InfoRefsControllerFacts
    {
        [Fact]
        public void InfoRefsControllerReturns403IfScmIsNotAvailable()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType")).Returns("None");

            var request = new HttpRequestMessage();
            InfoRefsController controller = CreateController(settings: settings.Object);
            controller.Request = request;

            // Act
            var response = controller.Execute("upload-pack");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public void InfoRefsControllerThrowsIfPushingAGitRepositoryWhenHgRepositoryIsAlreadyPresent()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType")).Returns("Git");

            var exception = new Exception();
            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            repositoryFactory.Setup(s => s.EnsureRepository(RepositoryType.Git)).Throws(exception);

            InfoRefsController controller = CreateController(settings: settings.Object, repositoryFactory: repositoryFactory.Object);

            // Act and Assert
            var ex = Assert.Throws<Exception>(() => controller.Execute("upload-pack"));

            // Assert
            Assert.Equal(exception, ex);
        }

        private InfoRefsController CreateController(IGitServer gitServer = null,
                                                    IDeploymentManager deploymentManager = null,
                                                    IDeploymentSettingsManager settings = null,
                                                    IRepositoryFactory repositoryFactory = null)
        {
            return new InfoRefsController(Mock.Of<ITracer>(),
                                          gitServer ?? Mock.Of<IGitServer>(),
                                          deploymentManager ?? Mock.Of<IDeploymentManager>(),
                                          settings ?? Mock.Of<IDeploymentSettingsManager>(),
                                          Mock.Of<IEnvironment>(),
                                          repositoryFactory ?? Mock.Of<IRepositoryFactory>());
        }
    }
}
