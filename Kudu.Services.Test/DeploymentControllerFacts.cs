using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Deployment;
using Moq;
using Newtonsoft.Json.Linq;
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
            opLock.Setup(f => f.Lock(It.IsAny<string>())).Returns(true);
            var controller = new DeploymentController(Mock.Of<ITracer>(), Mock.Of<IEnvironment>(), Mock.Of<IAnalytics>(), Mock.Of<IDeploymentManager>(), Mock.Of<IDeploymentStatusManager>(),
                                                      Mock.Of<IDeploymentSettingsManager>(), opLock.Object, repoFactory.Object);
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
            opLock.Setup(f => f.Lock(It.IsAny<string>())).Returns(true);
            var controller = new DeploymentController(Mock.Of<ITracer>(), Mock.Of<IEnvironment>(), Mock.Of<IAnalytics>(), Mock.Of<IDeploymentManager>(), Mock.Of<IDeploymentStatusManager>(),
                                                      Mock.Of<IDeploymentSettingsManager>(), opLock.Object, repoFactory.Object);
            controller.Request = GetRequest();

            // Act
            var response = await Assert.ThrowsAsync<HttpResponseException>(async () => await controller.Deploy("1234"));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
            Assert.Equal(@"{""Message"":""Deployment '1234' not found.""}", await response.Response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GetLogEntryDetailsReturns404IfLogEntryDetailsDoesNotExist()
        {
            // Arrange
            var deploymentManager = new Mock<IDeploymentManager>();
            deploymentManager
                .Setup(d => d.GetLogEntryDetails(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Enumerable.Empty<LogEntry>());
            var controller = new DeploymentController(Mock.Of<ITracer>(), Mock.Of<IEnvironment>(), Mock.Of<IAnalytics>(), deploymentManager.Object, Mock.Of<IDeploymentStatusManager>(),
                                                      Mock.Of<IDeploymentSettingsManager>(), Mock.Of<IOperationLock>(), Mock.Of<IRepositoryFactory>());
            controller.Request = GetRequest();

            // Act
            var response = controller.GetLogEntryDetails("deploymentId", "logId");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(@"{""Message"":""LogId 'logId' was not found in Deployment 'deploymentId'.""}", await response.Content.ReadAsStringAsync());
        }


        private static HttpRequestMessage GetRequest()
        {
            var request = new HttpRequestMessage();
            string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes("User:Pass"));
            request.Headers.Authorization = new AuthenticationHeaderValue("BASIC", authHeader);

            return request;
        }

        [Theory]
        [MemberData("ParseDeployResultScenarios")]
        public void TryParseDeployResultTests(string id, JObject payload, bool expected)
        {
            // Arrange
            var controller = new DeploymentController(Mock.Of<ITracer>(), Mock.Of<IEnvironment>(), Mock.Of<IAnalytics>(), Mock.Of<IDeploymentManager>(), Mock.Of<IDeploymentStatusManager>(),
                                                      Mock.Of<IDeploymentSettingsManager>(), Mock.Of<IOperationLock>(), Mock.Of<IRepositoryFactory>());

            // Act
            DeployResult result;
            var actual = controller.TryParseDeployResult(id, payload, out result);

            // Assert
            Assert.Equal(expected, actual);
            Assert.True(actual ? result != null : result == null);
            if (result != null)
            {
                Assert.Equal(id, result.Id);
                Assert.Equal((DeployStatus)payload.Value<int>("status"), result.Status);
                Assert.Equal(payload.Value<string>("message"), result.Message);
                Assert.Equal(payload.Value<string>("deployer"), result.Deployer);
                Assert.Equal(payload.Value<string>("author"), result.Author);
                Assert.Equal(payload.Value<string>("author_email"), result.AuthorEmail);
                Assert.NotNull(result.StartTime);
                Assert.NotNull(result.EndTime);

                var startTime = payload.Value<DateTime?>("start_time");
                if (startTime != null)
                {
                    Assert.Equal(startTime, result.StartTime);
                }

                var endTime = payload.Value<DateTime?>("end_time");
                if (endTime != null)
                {
                    Assert.Equal(endTime, result.EndTime);
                }

                var active = payload.Value<bool?>("active");
                if (active == null)
                {
                    Assert.Equal(result.Status == DeployStatus.Success, result.Current);
                }
                else
                {
                    Assert.Equal(active, result.Current);
                }
            }
        }

        public static IEnumerable<object[]> ParseDeployResultScenarios
        {
            get
            {
                yield return new object[] { null, null, false };
                yield return new object[] { "deploy_id", null, false };
                yield return new object[] { "deploy_id", new JObject(), false };
                yield return new object[] { "deploy_id", JObject.Parse("{status:2,message:'msg',deployer:'kudu',author:'me'}"), false };
                yield return new object[] { "deploy_id", JObject.Parse("{status:3,message:'',deployer:'kudu',author:'me'}"), false };
                yield return new object[] { "deploy_id", JObject.Parse("{status:4,message:'msg',deployer:'',author:'me'}"), false };
                yield return new object[] { "deploy_id", JObject.Parse("{status:3,message:'msg',deployer:'kudu',author:''}"), false };
                yield return new object[] { "deploy_id", JObject.Parse("{status:4,message:'msg',deployer:'kudu',author:'me'}"), true };

                var now = DateTime.UtcNow;
                yield return new object[] { "deploy_id", JObject.Parse("{status:3,message:'msg',deployer:'kudu',author:'me',author_email:'me@foo.com',start_time:'" + now.ToString("o") + "',end_time:'" + now.AddSeconds(25).ToString("o") + "'}"), true };
                yield return new object[] { "deploy_id", JObject.Parse("{status:4,message:'msg',deployer:'kudu',author:'me',author_email:'me@foo.com',start_time:'" + now.ToString("o") + "',end_time:'" + now.AddSeconds(25).ToString("o") + "'}"), true };
                yield return new object[] { "deploy_id", JObject.Parse("{status:4,message:'msg',deployer:'kudu',author:'me',author_email:'me@foo.com',start_time:'" + now.ToString("o") + "',end_time:'" + now.AddSeconds(25).ToString("o") + "',active:false}"), true };
            }
        }
    }
}
