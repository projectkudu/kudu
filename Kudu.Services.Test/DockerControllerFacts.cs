using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Docker;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class DockerControllerFacts
    {
        private readonly Mock<IDeploymentSettingsManager> _settingsManagerMock;
        private readonly Mock<ITraceFactory> _traceFactoryMock;
        private readonly Mock<IEnvironment> _environmentMock;
        private readonly DockerController _controller;
        private readonly MockHttpHandler _httpHandler;
        private string _dockerCIEnabled;

        public DockerControllerFacts()
        {
            _settingsManagerMock = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            _settingsManagerMock.Setup(p => p.GetValue(SettingsKeys.DockerCiEnabled, false)).Returns(() => _dockerCIEnabled);
            _traceFactoryMock = new Mock<ITraceFactory>(MockBehavior.Strict);
            _traceFactoryMock.Setup(p => p.GetTracer()).Returns(NullTracer.Instance);
            _environmentMock = new Mock<IEnvironment>(MockBehavior.Strict);
            _environmentMock.SetupGet(p => p.SiteRootPath).Returns(Path.Combine(Path.GetTempPath(), nameof(DockerControllerFacts)));

            _controller = new DockerController(_traceFactoryMock.Object, _settingsManagerMock.Object, _environmentMock.Object)
            {
                Request = new HttpRequestMessage()
            };

            _httpHandler = new MockHttpHandler();
            FunctionsHelper.HttpClient = new HttpClient(_httpHandler);
        }

        [Fact]
        public async Task ReceiveHook_InvokesSyncTriggers()
        {
            var prevIsolationValue = System.Environment.GetEnvironmentVariable("WEBSITE_ISOLATION");
            var prevHostValue = System.Environment.GetEnvironmentVariable(Constants.HttpHost);
            var prevEncryptionKey = System.Environment.GetEnvironmentVariable(Constants.SiteAuthEncryptionKey);

            _dockerCIEnabled = "1";

            var restartTriggerPath = DockerContainerRestartTrigger.GetRestartFilePath(_environmentMock.Object);
            if (File.Exists(restartTriggerPath))
            {
                File.Delete(restartTriggerPath);
            }
            
            try
            {
                System.Environment.SetEnvironmentVariable(Constants.SiteAuthEncryptionKey, GenerateKey());
                System.Environment.SetEnvironmentVariable("WEBSITE_ISOLATION", "hyperv");
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, "test.scm.azurewebsites.net");

                var response = await _controller.ReceiveHook();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                Assert.True(File.Exists(restartTriggerPath));

                var syncRequest = _httpHandler.Request;
                Assert.NotNull(syncRequest);
                Assert.Equal("https://test.azurewebsites.net/admin/host/synctriggers", syncRequest.RequestUri.ToString());
                Assert.Equal(PostDeploymentHelper.UserAgent.Value.ToString(), syncRequest.Headers.UserAgent.ToString());
                Assert.True(syncRequest.Headers.Contains(Constants.SiteRestrictedToken));
                Assert.True(syncRequest.Headers.Contains(Constants.RequestIdHeader));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("WEBSITE_ISOLATION", prevIsolationValue);
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, prevHostValue);
                System.Environment.SetEnvironmentVariable(Constants.SiteAuthEncryptionKey, prevEncryptionKey);
            }
        }

        private class MockHttpHandler : HttpClientHandler
        {
            public MockHttpHandler()
            {
            }

            public HttpRequestMessage Request { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                return Task.FromResult(response);
            }
        }

        private static string GenerateKey()
        {
            using (var aes = new AesManaged())
            {
                aes.GenerateKey();
                return BitConverter.ToString(aes.Key).Replace("-", string.Empty);
            }
        }
    }
}