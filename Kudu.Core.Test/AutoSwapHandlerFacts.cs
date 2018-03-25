using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
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
            var homePath = System.Environment.GetEnvironmentVariable("HOME");
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                System.Environment.SetEnvironmentVariable("HOME", tempPath);
                System.Environment.SetEnvironmentVariable(Constants.WebSiteSwapSlotName, null);

                var autoSwapLockFile = Path.Combine(tempPath, @"site\locks", PostDeploymentHelper.AutoSwapLockFile);
                Directory.CreateDirectory(Path.GetDirectoryName(autoSwapLockFile));

                Assert.False(PostDeploymentHelper.IsAutoSwapEnabled(), "Autoswap should NOT be enabled");
                Assert.False(PostDeploymentHelper.IsAutoSwapOngoing(), "Should not be any autoswap, since it is not enabled");

                System.Environment.SetEnvironmentVariable(Constants.WebSiteSwapSlotName, "someslot");
                Assert.True(PostDeploymentHelper.IsAutoSwapEnabled(), "Autoswap should be enabled");
                Assert.False(PostDeploymentHelper.IsAutoSwapOngoing(), "Should not be any autoswap, since autoswap lock is not acquired by process.");

                File.WriteAllText(autoSwapLockFile, string.Empty);
                File.SetLastWriteTimeUtc(autoSwapLockFile, DateTime.UtcNow.AddMinutes(-3));
                Assert.False(PostDeploymentHelper.IsAutoSwapOngoing(), "Should not be any autoswap, since autoswap lock is acquired over 2 mintues ago.");

                File.WriteAllText(autoSwapLockFile, string.Empty);
                Assert.True(PostDeploymentHelper.IsAutoSwapOngoing(), "Autoswap is ongoing, since autoswap lock is acquired within 2 mintues");
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("HOME", homePath);
                System.Environment.SetEnvironmentVariable(Constants.WebSiteSwapSlotName, null);
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
            }
        }

        [Fact]
        public async Task HandleAutoSwapTests()
        {
            var homePath = System.Environment.GetEnvironmentVariable("HOME");
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, null);
                System.Environment.SetEnvironmentVariable("HOME", tempPath);
                System.Environment.SetEnvironmentVariable(Constants.WebSiteSwapSlotName, null);

                var autoSwapLockFile = Path.Combine(tempPath, @"site\locks", PostDeploymentHelper.AutoSwapLockFile);

                string deploymentId = Guid.Empty.ToString();

                var tracerMock = new Mock<ITracer>();

                var traceListener = new PostDeploymentTraceListener(tracerMock.Object, Mock.Of<ILogger>());
                
                TestTracer.Trace("Autoswap will not happen, since it is not enabled.");
                await PostDeploymentHelper.PerformAutoSwap(string.Empty, traceListener);
                tracerMock.Verify(l => l.Trace("AutoSwap is not enabled", It.IsAny<IDictionary<string, string>>()), Times.Once);

                TestTracer.Trace("Autoswap will not happen, since there is no HTTP_HOST env.");
                System.Environment.SetEnvironmentVariable(Constants.WebSiteSwapSlotName, "someslot");
                string jwtToken = Guid.NewGuid().ToString();
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => PostDeploymentHelper.PerformAutoSwap(string.Empty, traceListener));
                Assert.Equal("Missing HTTP_HOST env!", exception.Message);

                string hostName = "foo.scm.bar";
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, hostName);
                System.Environment.SetEnvironmentVariable(Constants.SiteAuthEncryptionKey, GenerateKey());

                TestTracer.Trace("Autoswap will be triggered");
                string newDeploymentId = Guid.NewGuid().ToString();

                string autoSwapRequestUrl = null;
                string swtToken = null;
                PostDeploymentHelper.HttpClientFactory = () => new HttpClient(new TestMessageHandler((HttpRequestMessage requestMessage) =>
                {
                    autoSwapRequestUrl = requestMessage.RequestUri.AbsoluteUri;
                    swtToken = requestMessage.Headers.GetValues(Constants.SiteRestrictedToken).First();
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

                Assert.True(!File.Exists(autoSwapLockFile), string.Format("File {0} should not exist.", autoSwapLockFile));

                await PostDeploymentHelper.PerformAutoSwap(string.Empty, traceListener);

                Assert.True(File.Exists(autoSwapLockFile), string.Format("File {0} should exist.", autoSwapLockFile));

                Assert.NotNull(autoSwapRequestUrl);
                Assert.True(autoSwapRequestUrl.StartsWith("https://foo.scm.bar/operations/autoswap?slot=someslot&operationId=AUTOSWAP"));

                Assert.NotNull(swtToken);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(Constants.HttpHost, null);
                System.Environment.SetEnvironmentVariable("HOME", homePath);
                System.Environment.SetEnvironmentVariable(Constants.WebSiteSwapSlotName, null);
                System.Environment.SetEnvironmentVariable(Constants.SiteAuthEncryptionKey, null);
                PostDeploymentHelper.HttpClientFactory = null;
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
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
