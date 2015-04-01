using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Services.Deployment;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class DeploymentControllerFacts
    {
        [Fact]
        public async Task RedeployReturns404IfRepoDoesNotExist()
        {
            // Arrange
            var repoFactory = new Mock<IRepositoryFactory>();
            repoFactory.Setup(r => r.GetRepository()).Returns((IRepository)null);
            var opLock = new Mock<IOperationLock>();
            opLock.Setup(f => f.Lock()).Returns(true);
            var controller = new DeploymentController(Mock.Of<ITracer>(), Mock.Of<IDeploymentManager>(), Mock.Of<IDeploymentStatusManager>(),
                                                      opLock.Object, repoFactory.Object, Mock.Of<IAutoSwapHandler>());
            controller.Request = GetRequest();

            // Act
            var response = await Assert.ThrowsAsync<HttpResponseException>(async () => await controller.Deploy());

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
            Assert.Equal(@"{""Message"":""Repository could not be found.""}", await response.Response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task RedeployReturns404IfCommitDoesNotExist()
        {
            // Arrange
            var repository = new Mock<IRepository>();
            repository.Setup(r => r.GetChangeSet(It.IsAny<string>()))
                      .Returns((ChangeSet)null);
            var repoFactory = new Mock<IRepositoryFactory>();
            repoFactory.Setup(r => r.GetRepository()).Returns(repository.Object);
            var opLock = new Mock<IOperationLock>();
            opLock.Setup(f => f.Lock()).Returns(true);
            var controller = new DeploymentController(Mock.Of<ITracer>(), Mock.Of<IDeploymentManager>(), Mock.Of<IDeploymentStatusManager>(),
                                                      opLock.Object, repoFactory.Object, Mock.Of<IAutoSwapHandler>());
            controller.Request = GetRequest();

            // Act
            var response = await Assert.ThrowsAsync<HttpResponseException>(async () => await controller.Deploy("1234"));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
            Assert.Equal(@"{""Message"":""Deployment '1234' not found.""}", await response.Response.Content.ReadAsStringAsync());
        }

        private static HttpRequestMessage GetRequest()
        {
            var request = new HttpRequestMessage();
            string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes("User:Pass"));
            request.Headers.Authorization = new AuthenticationHeaderValue("BASIC", authHeader);

            return request;
        }
    }
}
