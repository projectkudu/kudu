using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
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

        [Fact]
        public async Task ApplyChangesCoreDeletesFilesForDeltasThatHaveBeenDeleted()
        {
            // Arrange
            var helper = CreateDropboxHelper();
            var fileDeltaInfo = new DropboxDeltaInfo { Path = "foo/bar.txt", IsDeleted = true };
            var dirDeltaInfo = new DropboxDeltaInfo { Path = "foo/baz/", IsDeleted = true, IsDirectory = true };
            var deployInfo = new DropboxDeployInfo { Path = "foo" };
            deployInfo.Deltas = new[] { fileDeltaInfo, dirDeltaInfo };
            string filePath = Path.Combine(helper.Environment.RepositoryPath, "bar.txt"),
                   dirPath = Path.Combine(helper.Environment.RepositoryPath, "baz");

            File.WriteAllBytes(filePath, new byte[0]);
            Directory.CreateDirectory(dirPath);

            // Act
            await helper.ApplyChangesCore(deployInfo);

            // Assert
            Assert.False(File.Exists(filePath));
            Assert.False(Directory.Exists(dirPath));
        }

        [Fact]
        public async Task ApplyChangesCoreCreatesDirectoriesForDirectoryDeltas()
        {
            // Arrange
            var helper = CreateDropboxHelper();
            var dirDeltaInfo = new DropboxDeltaInfo { Path = "foo/qux/", IsDirectory = true };
            var deployInfo = new DropboxDeployInfo { Path = "foo" };
            deployInfo.Deltas = new[] { dirDeltaInfo };
            string dirPath = Path.Combine(helper.Environment.RepositoryPath, "qux");

            // Act
            await helper.ApplyChangesCore(deployInfo);

            // Assert
            Assert.True(Directory.Exists(dirPath));
        }

        [Fact]
        public async Task ApplyChangesCoreThrowsIfAnyFileTaskFails()
        {
            // Arrange
            var helper = CreateDropboxHelper();
            int processedFiles = 0;
            Mock.Get(helper).Setup(h => h.ProcessFileAsync(It.IsAny<DropboxDeployInfo>(), It.IsAny<DropboxDeltaInfo>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                            .Callback(() => Interlocked.Increment(ref processedFiles))
                            .Returns(Task.FromResult(0));

            Mock.Get(helper).Setup(h => h.ProcessFileAsync(It.IsAny<DropboxDeployInfo>(), It.IsAny<DropboxDeltaInfo>(), It.IsAny<string>(), "foo/qux.txt", It.IsAny<DateTime>()))
                            .Callback(() => Interlocked.Increment(ref processedFiles))
                            .Returns(GetFailedTask());
            var deployInfo = new DropboxDeployInfo { Path = "foo" };
            deployInfo.Deltas = new[] 
            { 
                new DropboxDeltaInfo { Path = "foo/test.txt" },
                new DropboxDeltaInfo { Path = "foo/bar.txt" },
                new DropboxDeltaInfo { Path = "foo/qux.txt" },
                new DropboxDeltaInfo { Path = "foo/buzz.png" },
                new DropboxDeltaInfo { Path = "foo/baz.php"},
                new DropboxDeltaInfo { Path = "foo/file0.php"},
                new DropboxDeltaInfo { Path = "foo/file1.php"},
                new DropboxDeltaInfo { Path = "foo/file2.php"},
                new DropboxDeltaInfo { Path = "foo/file3.php"},
            };

            // Act
            await ExceptionAssert.ThrowsAsync<HttpRequestException>(async() => await helper.ApplyChangesCore(deployInfo));

            // Assert
            // Ensure we processed other files
            Assert.Equal(deployInfo.Deltas.Count, processedFiles);
        }

        private DropboxHelper CreateDropboxHelper(TestMessageHandler handler = null)
        {
            string repositoryPath = Path.Combine(_testRoot, Path.GetRandomFileName());
            EnsureDirectory(repositoryPath);
            var env = new Mock<IEnvironment>();
            env.SetupGet(e => e.RepositoryPath).Returns(repositoryPath);
            var helper = new Mock<DropboxHelper>(Mock.Of<ITracer>(), Mock.Of<IDeploymentStatusManager>(), Mock.Of<IDeploymentSettingsManager>(), env.Object);
            helper.CallBase = true;
            helper.Object.Logger = Mock.Of<ILogger>();

            if (handler != null)
            {
                helper.Setup(h => h.CreateDropboxHttpClient())
                      .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://site-does-not-exist.microsoft.com") });

                DropboxHelper.RetryWaitToAvoidRateLimit = TimeSpan.FromSeconds(1);
            }
            return helper.Object;
        }

        private static Task GetFailedTask()
        {
            var tcs = new TaskCompletionSource<int>();
            tcs.TrySetException(new HttpRequestException());
            return tcs.Task;
        }

        private static void EnsureDirectory(string repositoryPath)
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
