using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Test;
using Kudu.TestHarness;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class AutoSwapHandlerFacts
    {
        [Fact]
        public void IsAutoSwapOngoingOrEnabledTests()
        {
            var deploymentSettingsMock = new Mock<IDeploymentSettingsManager>();
            var enviromentMock = new Mock<IEnvironment>();
            var traceFactoryMock = new Mock<ITraceFactory>();
            traceFactoryMock.Setup(s => s.GetTracer()).Returns(Mock.Of<ITracer>());

            enviromentMock.Setup(e => e.LocksPath).Returns(@"x:\foo");
            var handler = new AutoSwapHandler(
                Mock.Of<IDeploymentStatusManager>(),
                enviromentMock.Object,
                deploymentSettingsMock.Object,
                traceFactoryMock.Object);

            Assert.False(handler.IsAutoSwapEnabled(), "Autoswap should NOT be enabled");
            Assert.False(handler.IsAutoSwapOngoing(), "Should not be any autoswap, since it is not enabled");

            deploymentSettingsMock.Setup(
                s => s.GetValue(It.Is<string>(v => "WEBSITE_SWAP_SLOTNAME".StartsWith(v)), It.IsAny<bool>())
            ).Returns("someslot");

            handler = new AutoSwapHandler(
                Mock.Of<IDeploymentStatusManager>(),
                enviromentMock.Object,
                deploymentSettingsMock.Object,
                traceFactoryMock.Object);

            Assert.True(handler.IsAutoSwapEnabled(), "Autoswap should be enabled");

            var fileSystemMock = new Mock<IFileSystem>();
            var fileInfoMock = new Mock<IFileInfoFactory>();
            var fileInfoBaseMock = new Mock<FileInfoBase>();
            fileSystemMock.Setup(f => f.FileInfo).Returns(fileInfoMock.Object);
            fileInfoMock.Setup(f => f.FromFileName(It.IsAny<string>())).Returns(fileInfoBaseMock.Object);
            fileInfoBaseMock.Setup(f => f.Exists).Returns(false);
            fileInfoBaseMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
            FileSystemHelpers.Instance = fileSystemMock.Object;

            Assert.False(handler.IsAutoSwapOngoing(), "Should not be any autoswap, since autoswap lock is not acquired by process.");

            fileInfoBaseMock.Setup(f => f.Exists).Returns(true);
            fileInfoBaseMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow.AddMinutes(-3));

            Assert.False(handler.IsAutoSwapOngoing(), "Should not be any autoswap, since autoswap lock is acquired over 2 mintues ago.");

            fileInfoBaseMock.Setup(f => f.Exists).Returns(true);
            fileInfoBaseMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

            Assert.True(handler.IsAutoSwapOngoing(), "Autoswap is ongoing, since autoswap lock is acquired within 2 mintues");
        }

        [Fact]
        public async Task HandleAutoSwapTests()
        {
            string deploymentId = Guid.Empty.ToString();

            var deploymentSettingsMock = new Mock<IDeploymentSettingsManager>();
            var enviromentMock = new Mock<IEnvironment>();
            var deploymentStatusManagerMock = new Mock<IDeploymentStatusManager>();
            var tracerMock = new Mock<ITracer>();
            var deploymentContextMock = new DeploymentContext()
            {
                Logger = Mock.Of<ILogger>(),
                Tracer = tracerMock.Object
            };

            enviromentMock.Setup(e => e.LocksPath).Returns(@"x:\foo");
            deploymentStatusManagerMock.Setup(d => d.ActiveDeploymentId).Returns(deploymentId);

            var handler = new AutoSwapHandler(
                deploymentStatusManagerMock.Object,
                enviromentMock.Object,
                deploymentSettingsMock.Object,
                Mock.Of<ITraceFactory>());

            TestTracer.Trace("Autoswap will not happen, since it is not enabled.");
            await handler.HandleAutoSwap(deploymentId, deploymentContextMock);

            TestTracer.Trace("Autoswap will not happen, since there is no JWT token.");
            System.Environment.SetEnvironmentVariable(Constants.SiteRestrictedJWT, null);
            deploymentSettingsMock.Setup(
                s => s.GetValue(It.Is<string>(v => "WEBSITE_SWAP_SLOTNAME".StartsWith(v)), It.IsAny<bool>())
            ).Returns("someslot");

            handler = new AutoSwapHandler(
                deploymentStatusManagerMock.Object,
                enviromentMock.Object,
                deploymentSettingsMock.Object,
                Mock.Of<ITraceFactory>());

            var fileSystemMock = new Mock<IFileSystem>();
            var fileInfoMock = new Mock<IFileInfoFactory>();
            var fileInfoBaseMock = new Mock<FileInfoBase>();
            FileSystemHelpers.Instance = fileSystemMock.Object;

            fileSystemMock.Setup(f => f.FileInfo).Returns(fileInfoMock.Object);
            fileInfoMock.Setup(f => f.FromFileName(It.IsAny<string>())).Returns(fileInfoBaseMock.Object);
            fileInfoBaseMock.Setup(f => f.Exists).Returns(true);
            fileInfoBaseMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
            await handler.HandleAutoSwap(deploymentId, deploymentContextMock);

            try
            {
                string jwtToken = Guid.NewGuid().ToString();
                string hostName = "foo.scm.bar";
                System.Environment.SetEnvironmentVariable(Constants.SiteRestrictedJWT, jwtToken);
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, hostName);

                TestTracer.Trace("Autoswap will not happen, since there deploymet id not changed");
                await handler.HandleAutoSwap(deploymentId, deploymentContextMock);

                tracerMock.Verify(l => l.Trace("AutoSwap is not enabled", It.IsAny<IDictionary<string, string>>()), Times.Once);
                tracerMock.Verify(l => l.Trace("AutoSwap is not enabled", It.IsAny<IDictionary<string, string>>()), Times.Once);
                tracerMock.Verify(l =>
                    l.Trace(string.Format(CultureInfo.InvariantCulture, "Deployment haven't changed, no need for auto swap: {0}", deploymentId),
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);

                TestTracer.Trace("Autoswap will be triggered");
                string newDeploymentId = Guid.NewGuid().ToString();

                string autoSwapRequestUrl = null;
                string bearerToken = null;
                OperationClient.ClientHandler = new TestMessageHandler((HttpRequestMessage requestMessage) =>
                {
                    autoSwapRequestUrl = requestMessage.RequestUri.AbsoluteUri;
                    bearerToken = requestMessage.Headers.GetValues("Authorization").First();
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

                await handler.HandleAutoSwap(newDeploymentId, deploymentContextMock);
                Assert.NotNull(autoSwapRequestUrl);
                Assert.True(autoSwapRequestUrl.StartsWith("https://foo.scm.bar/operations/autoswap?slot=someslot&operationId=AUTOSWAP"));

                Assert.NotNull(bearerToken);
                Assert.Equal("Bearer " + jwtToken, bearerToken);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(Constants.SiteRestrictedJWT, null);
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, null);
                OperationClient.ClientHandler = null;
            }
        }
    }
}
