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
        public void InfoRefsControllerDisallowsPushingAGitRepositoryWhenHgRepositoryIsAlreadyPresent()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType", false)).Returns("Git");

            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            var repository = new Mock<IRepository>();
            repository.SetupGet(r => r.RepositoryType).Returns(RepositoryType.Mercurial);
            repositoryFactory.Setup(s => s.GetRepository()).Returns(repository.Object);

            var request = new HttpRequestMessage();
            InfoRefsController controller = CreateController(settings: settings.Object, repositoryFactory: repositoryFactory.Object);
            controller.Request = request;

            // Act and Assert
            var response = controller.Execute("upload-pack");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private InfoRefsController CreateController(IGitServer gitServer = null,
                                                    IDeploymentManager deploymentManager = null,
                                                    IDeploymentSettingsManager settings = null,
                                                    IRepositoryFactory repositoryFactory = null)
        {
            return new InfoRefsController(Mock.Of<ITracer>(), gitServer ?? Mock.Of<IGitServer>(), repositoryFactory ?? Mock.Of<IRepositoryFactory>());
        }
    }
}