using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class DropboxHelperFacts : IDisposable
    {
        private readonly string _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        [Fact]
        public async Task ProcessFileAsyncWritesFileToSpecifiedPath()
        {
            // Arrange
            string content = "Hello world!";
            string path = "foo/bar/test.txt";
            var handler = new TestMessageHandler(content);
            var helper = CreateDropboxHelper(handler);

            // Act
            await helper.ProcessFileAsync(new DropboxDeployInfo(), new DropboxDeltaInfo { Path = path }, @"foo/bar/", path, DateTime.UtcNow);

            // Assert
            string expectedFile = Path.Combine(helper.Environment.RepositoryPath, "test.txt");
            Assert.Equal(content, File.ReadAllText(expectedFile));
        }

        [Fact]
        public async Task ProcessFileAsyncRetriesTwice()
        {
            // Arrange
            string content = "Hello world! This is a new test!";
            string path = "foo/qux/test.txt";
            int counter = 0;
            var handler = new TestMessageHandler(request =>
            {
                counter++;
                var response = new HttpResponseMessage();
                if (counter < 3)
                {
                    response.StatusCode = HttpStatusCode.Forbidden;
                }
                else
                {
                    response.Content = new StringContent(content);
                }
                return response;
            });
            var helper = CreateDropboxHelper(handler);

            // Act
            await helper.ProcessFileAsync(new DropboxDeployInfo(), new DropboxDeltaInfo { Path = path }, @"foo/qux/", path, DateTime.UtcNow);

            // Assert
            string expectedFile = Path.Combine(helper.Environment.RepositoryPath, "test.txt");
            Assert.Equal(content, File.ReadAllText(expectedFile));
            Assert.Equal(3, counter);
        }

        [Fact]
        public async Task ProcessFileAsyncThrowsIfFailedAfterThreeRetries()
        {
            // Arrange
            string path = "foo/qux/test.txt";
            int counter = 0;
            var handler = new TestMessageHandler(_ => 
            {
                counter++;
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            });
          
            var helper = CreateDropboxHelper(handler);

            // Act
            await ExceptionAssert.ThrowsAsync<HttpRequestException>(async() => 
                await helper.ProcessFileAsync(new DropboxDeployInfo(), new DropboxDeltaInfo { Path = path }, @"foo/qux/", path, DateTime.UtcNow));

            // Assert
            Assert.Equal(3, counter);
        }

        private DropboxHelper CreateDropboxHelper(TestMessageHandler handler)
        {
            string repositoryPath = Path.Combine(_testRoot, Path.GetRandomFileName());
            EnsureDirectory(repositoryPath);
            var env = new Mock<IEnvironment>();
            env.SetupGet(e => e.RepositoryPath).Returns(repositoryPath);
            var helper = new Mock<DropboxHelper>(Mock.Of<ITracer>(), Mock.Of<IDeploymentStatusManager>(), Mock.Of<IDeploymentSettingsManager>(), env.Object);
            helper.Setup(h => h.CreateDropboxHttpClient())
                  .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://site-does-not-exist.microsoft.com") });
            helper.Object.Logger = Mock.Of<ILogger>();

            DropboxHelper.RetryWaitToAvoidRateLimit = TimeSpan.FromSeconds(1);
            return helper.Object;
        }

        private void EnsureDirectory(string repositoryPath)
        {
            if (!Directory.Exists(repositoryPath))
            {
                Directory.CreateDirectory(repositoryPath);
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_testRoot, recursive: true);
            }
            catch
            {
                
            }
        }
    }
}
