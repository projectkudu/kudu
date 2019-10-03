using System;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Deployment;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Deployment
{
    public class PushDeploymentControllerFacts
    {
        [Fact]
        public async Task ZipDeployInRunFromZipSetsZipNameAndSyncTriggersPath()
        {
            // Setup
            var environment = new Mock<IEnvironment>();
            var deploymentManager = new Mock<IFetchDeploymentManager>();
            var tracer = new Mock<ITracer>();
            var traceFactory = new Mock<ITraceFactory>();
            var settings = new Mock<IDeploymentSettingsManager>();
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var directoryBase = new Mock<DirectoryBase>();
            var fileStream = new MemoryStream();
            var requestStream = new MemoryStream(Encoding.UTF8.GetBytes("Request Body"));
            var streamContent = new StreamContent(requestStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            settings
                .Setup(s => s.GetValue(It.Is<string>(t => t == SettingsKeys.RunFromZip), It.IsAny<bool>()))
                .Returns("1");

            environment
                .SetupGet(e => e.SitePackagesPath)
                .Returns(@"x:\data");

            fileBase
                .Setup(f => f.Create(It.IsAny<string>()))
                .Returns(fileStream);
            directoryBase
                .Setup(d => d.Exists(It.IsAny<string>()))
                .Returns(true);

            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directoryBase.Object);

            FileSystemHelpers.Instance = fileSystem.Object;

            deploymentManager
                .Setup(m => m.FetchDeploy(It.IsAny<ZipDeploymentInfo>(), It.IsAny<bool>(), It.IsAny<Uri>(), It.IsAny<string>()))
                .Returns(Task.FromResult(FetchDeploymentRequestResult.RanSynchronously));

            var controller = new PushDeploymentController(
                environment.Object,
                deploymentManager.Object,
                tracer.Object,
                traceFactory.Object,
                settings.Object)
            {
                Request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/zipDeploy")
                {
                    Content = streamContent
                }
            };

            // Act
            var response = await controller.ZipPushDeploy();

            // Assert
            deploymentManager
                .Verify(m => m.FetchDeploy(It.Is<ZipDeploymentInfo>(di =>
                    !string.IsNullOrEmpty(di.ZipName) && !string.IsNullOrEmpty(di.SyncFunctionsTriggersPath)),
                    It.IsAny<bool>(),
                    It.IsAny<Uri>(),
                    It.IsAny<string>()
                ));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(
            "https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites/site-name/extensions/zipdeploy?api-version=2016-03-01",
            "https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites/site-name"
            )]
        [InlineData(
            "https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites/site-name/slots/staging/extensions/zipdeploy?api-version=2016-03-01",
            "https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites/site-name/slots/staging"
            )]
        [InlineData(
            "https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites",
            null
            )]
        public void ZipDeployGetAsyncLocation(string referer, string expected)
        {
            var actual = PushDeploymentController.GetStatusUrl(new Uri(referer));
            Assert.Equal(expected, actual);
        }
    }
}
