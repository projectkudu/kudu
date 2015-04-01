using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
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
            await helper.ProcessFileAsync(CreateHttpClient(handler), path, @"foo/bar/", DateTime.UtcNow);

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
            await helper.ProcessFileAsync(CreateHttpClient(handler), path, @"foo/qux/", DateTime.UtcNow);

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
            await Assert.ThrowsAsync<HttpRequestException>(async() =>
                await helper.ProcessFileAsync(CreateHttpClient(handler), path, @"foo/qux/", DateTime.UtcNow));

            // Assert
            Assert.Equal(3, counter);
        }

        [Fact]
        public async Task ApplyChangesCoreDeletesFilesForDeltasThatHaveBeenDeleted()
        {
            // Arrange
            var helper = CreateDropboxHelper();
            var fileDeltaInfo = new DropboxEntryInfo { Path = "foo/bar.txt", IsDeleted = true };
            var dirDeltaInfo = new DropboxEntryInfo { Path = "foo/baz/", IsDeleted = true, IsDirectory = true };
            var deployInfo = new DropboxDeployInfo { Path = "foo" };
            deployInfo.Deltas.AddRange(new [] { fileDeltaInfo, dirDeltaInfo });
            string filePath = Path.Combine(helper.Environment.RepositoryPath, "bar.txt"),
                   dirPath = Path.Combine(helper.Environment.RepositoryPath, "baz");

            File.WriteAllBytes(filePath, new byte[0]);
            Directory.CreateDirectory(dirPath);

            // Act
            await helper.ApplyChangesCore(deployInfo, useOAuth20: false);

            // Assert
            Assert.False(File.Exists(filePath));
            Assert.False(Directory.Exists(dirPath));
        }

        [Fact]
        public async Task ApplyChangesCoreCreatesDirectoriesForDirectoryDeltas()
        {
            // Arrange
            var helper = CreateDropboxHelper();
            var dirDeltaInfo = new DropboxEntryInfo { Path = "foo/qux/", IsDirectory = true };
            var deployInfo = new DropboxDeployInfo { Path = "foo" };
            deployInfo.Deltas.Add(dirDeltaInfo);
            string dirPath = Path.Combine(helper.Environment.RepositoryPath, "qux");

            // Act
            await helper.ApplyChangesCore(deployInfo, useOAuth20: false);

            // Assert
            Assert.True(Directory.Exists(dirPath));
        }

        [Fact]
        public async Task ApplyChangesCoreThrowsIfAnyFileTaskFails()
        {
            // Arrange
            var helper = CreateDropboxHelper();
            int processedFiles = 0;
            Mock.Get(helper).Setup(h => h.ProcessFileAsync(It.IsAny<HttpClient>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                            .Callback(() => Interlocked.Increment(ref processedFiles))
                            .Returns(Task.FromResult(0));

            Mock.Get(helper).Setup(h => h.ProcessFileAsync(It.IsAny<HttpClient>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                            .Callback(() => Interlocked.Increment(ref processedFiles))
                            .Returns(GetFailedTask());
            var deployInfo = new DropboxDeployInfo { Path = "foo" };
            deployInfo.Deltas.AddRange(new [] 
            { 
                new DropboxEntryInfo { Path = "foo/test.txt" },
                new DropboxEntryInfo { Path = "foo/bar.txt" },
                new DropboxEntryInfo { Path = "foo/qux.txt" },
                new DropboxEntryInfo { Path = "foo/buzz.png" },
                new DropboxEntryInfo { Path = "foo/baz.php"},
                new DropboxEntryInfo { Path = "foo/file0.php"},
                new DropboxEntryInfo { Path = "foo/file1.php"},
                new DropboxEntryInfo { Path = "foo/file2.php"},
                new DropboxEntryInfo { Path = "foo/file3.php"},
            });

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () => await helper.ApplyChangesCore(deployInfo, useOAuth20: false));

            // Assert
            // Ensure we processed other files
            Assert.Equal(deployInfo.Deltas.Count, processedFiles);
        }

        [Fact]
        public async Task GetDeltasPopulatesEntriesNode()
        {
            // Arrange
            const string DeltaPayload = @"{""reset"": true, ""cursor"": ""AAGWKUylpghsuMRcKQSdHSpvUW3uPVcIyGINt30oO36wDebzBoqtFFaiqzNCWV568-U_uZwdM1QGyzxYw3GxJRsCWv0G3BlOUiguFrttRsbpmA"", ""has_more"": false, ""entries"": [[""/foo/bar.txt"", {""revision"": 1, ""rev"": ""11357a5a5"", ""thumb_exists"": false, ""bytes"": 123641, ""modified"": ""Thu, 22 Aug 2013 22:50:24 +0000"", ""client_mtime"": ""Thu, 22 Aug 2013 22:50:24 +0000"", ""path"": ""/foo/bar.txt"", ""is_dir"": false, ""icon"": ""page_white"", ""root"": ""dropbox"", ""mime_type"": ""application/epub+zip"", ""size"": ""120.7 KB""}]]}";
            var dropboxInfo = new DropboxDeployInfo
            {
                Token = "Some-token",
                Path = "/foo"
            };
            var handler = new TestMessageHandler(DeltaPayload, isJson: true);
            var helper = CreateDropboxHelper(handler);

            // Act
            await helper.UpdateDropboxDeployInfo(dropboxInfo);

            // Assert
            Assert.Null(dropboxInfo.OldCursor);
            Assert.Equal("AAGWKUylpghsuMRcKQSdHSpvUW3uPVcIyGINt30oO36wDebzBoqtFFaiqzNCWV568-U_uZwdM1QGyzxYw3GxJRsCWv0G3BlOUiguFrttRsbpmA", dropboxInfo.NewCursor);
            Assert.Equal(1, dropboxInfo.Deltas.Count);
            Assert.Equal("/foo/bar.txt", dropboxInfo.Deltas[0].Path);
        }

        [Fact]
        public async Task GetDeltasPopulatesEntriesNodeWhenHasMoreIsSet()
        {
            // Arrange
            const string DeltaPayload1 = @"{""reset"": true, ""cursor"": ""cursor1"", ""has_more"": true, ""entries"": [[""/foo/bar.txt"", {""revision"": 1, ""rev"": ""11357a5a5"", ""bytes"": 123641, ""modified"": ""Thu, 22 Aug 2013 22:50:24 +0000"", ""client_mtime"": ""Thu, 22 Aug 2013 22:50:24 +0000"", ""path"": ""/foo/bar.txt"", ""is_dir"": false}]]}";
            const string DeltaPayload2 = @"{""reset"": false, ""cursor"": ""cursor2"", ""has_more"": false, ""entries"": [[""/foo/bar.txt"", {""revision"": 1, ""rev"": ""11357a5a5"", ""bytes"": 123641, ""modified"": ""Thu, 22 Aug 2013 22:50:24 +0000"", ""client_mtime"": ""Thu, 22 Aug 2013 22:50:24 +0000"", ""path"": ""/foo/qux.txt"", ""is_dir"": false}]]}";
            var dropboxInfo = new DropboxDeployInfo
            {
                Token = "Some-token",
                Path = "/foo"
            };
            int i = 0;
            var handler = new TestMessageHandler(_ => 
            {
                var content = new StringContent(i++ == 0 ? DeltaPayload1 : DeltaPayload2, Encoding.UTF8, "application/json");
                return new HttpResponseMessage { Content = content };
            });
            var helper = CreateDropboxHelper(handler);

            // Act
            await helper.UpdateDropboxDeployInfo(dropboxInfo);

            // Assert
            Assert.Equal("cursor2", dropboxInfo.NewCursor);
            Assert.Equal(2, dropboxInfo.Deltas.Count);
            Assert.Equal("/foo/bar.txt", dropboxInfo.Deltas[0].Path);
            Assert.Equal("/foo/qux.txt", dropboxInfo.Deltas[1].Path);
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
                helper.Setup(h => h.CreateDropboxHttpClient(It.IsAny<DropboxDeployInfo>(), It.IsAny<DropboxEntryInfo>()))
                      .Returns(CreateHttpClient(handler));

                helper.Setup(h => h.CreateDropboxV2HttpClient(It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(CreateHttpClient(handler));

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

        private static HttpClient CreateHttpClient(TestMessageHandler handler = null)
        {
            if (handler == null)
            {
                handler = new TestMessageHandler(HttpStatusCode.OK);
            }

            return new HttpClient(handler) { BaseAddress = new Uri("http://site-does-not-exist.microsoft.com") };
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
