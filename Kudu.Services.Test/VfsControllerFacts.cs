using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http.Hosting;
using System.Web.Http.Routing;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Services.Editor;
using Kudu.Services.Infrastructure;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class VfsControllerFacts
    {
        private const string SiteRootPath = @"C:\DWASFiles\Sites\SiteName\VirtualDirectory0";
        private static readonly string LocalSiteRootPath = Path.GetFullPath(Path.Combine(SiteRootPath, @".."));

        [Fact]
        public async Task DeleteRequestReturnsNotFoundIfItemDoesNotExist()
        {
            // Arrange
            string path = @"/foo/bar/";
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Attributes).Returns((FileAttributes)(-1));
            var dirInfo = new Mock<DirectoryInfoBase>();
            dirInfo.SetupGet(d => d.Attributes).Returns((FileAttributes)(-1));
            var fileSystem = CreateFileSystem(path, dirInfo.Object, fileInfo.Object);
            var controller = CreateController(path);
            FileSystemHelpers.Instance = fileSystem;

            // Act
            var result = await controller.DeleteItem();

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public async Task DeleteRequestDoesNotRecursivelyDeleteDirectoriesByDefault()
        {
            // Arrange
            string path = @"/foo/bar/";
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Attributes).Returns(FileAttributes.Directory);
            var dirInfo = new Mock<DirectoryInfoBase>();
            dirInfo.SetupGet(d => d.Attributes).Returns(FileAttributes.Directory);
            var fileSystem = CreateFileSystem(path, dirInfo.Object, fileInfo.Object);
            FileSystemHelpers.Instance = fileSystem;

            var controller = CreateController(path);

            // Act
            await controller.DeleteItem();

            // Assert
            dirInfo.Verify(d => d.Delete(false), Times.Once());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeleteRequestInvokesRecursiveDeleteBasedOnParameter(bool recursive)
        {
            // Arrange
            string path = @"/foo/bar/";
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Attributes).Returns(FileAttributes.Directory);
            var dirInfo = new Mock<DirectoryInfoBase>();
            dirInfo.SetupGet(d => d.Attributes).Returns(FileAttributes.Directory);
            var fileSystem = CreateFileSystem(path, dirInfo.Object, fileInfo.Object);
            var controller = CreateController(path);
            FileSystemHelpers.Instance = fileSystem;

            // Act
            var response = await controller.DeleteItem(recursive);

            // Assert
            dirInfo.Verify(d => d.Delete(recursive), Times.Once());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DeleteItemReturnsPreconditionFailedResponseIfFileDeleteDoesNotContainETag()
        {
            // Arrange
            string path = @"/foo/bar.txt";
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Attributes).Returns(FileAttributes.Normal);
            var dirInfo = new Mock<DirectoryInfoBase>();
            dirInfo.SetupGet(d => d.Attributes).Returns(FileAttributes.Normal);
            var fileSystem = CreateFileSystem(path, dirInfo.Object, fileInfo.Object);
            FileSystemHelpers.Instance = fileSystem;

            var controller = CreateController(path);

            // Act
            var response = await controller.DeleteItem();

            // Assert
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        }

        [Fact]
        public async Task DeleteItemReturnsPreconditionFailedIfETagDoesNotMatch()
        {
            // Arrange
            var date = new DateTime(2012, 07, 06);
            string path = @"/foo/bar.txt";
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Attributes).Returns(FileAttributes.Normal);
            fileInfo.SetupGet(f => f.LastWriteTimeUtc).Returns(date);
            var dirInfo = new Mock<DirectoryInfoBase>();
            dirInfo.SetupGet(d => d.Attributes).Returns(FileAttributes.Normal);
            var fileSystem = CreateFileSystem(path, dirInfo.Object, fileInfo.Object);
            FileSystemHelpers.Instance = fileSystem;

            var controller = CreateController(path);
            controller.Request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"this-will-not-match\""));

            // Act
            var response = await controller.DeleteItem();

            // Assert
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        }

        public static IEnumerable<object[]> DeleteItemDeletesFileIfETagMatchesData
        {
            get
            {
                yield return new object[] { EntityTagHeaderValue.Any };
                yield return new object[] { new EntityTagHeaderValue("\"00c0b16b2129cf08\"") };
            }
        }

        [Theory]
        [MemberData("DeleteItemDeletesFileIfETagMatchesData")]
        public async Task DeleteItemDeletesFileIfETagMatches(EntityTagHeaderValue etag)
        {
            // Arrange
            var date = new DateTime(2012, 07, 06);
            string path = @"/foo/bar.txt";
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Attributes).Returns(FileAttributes.Normal);
            fileInfo.SetupGet(f => f.LastWriteTimeUtc).Returns(date);
            var dirInfo = new Mock<DirectoryInfoBase>();
            dirInfo.SetupGet(d => d.Attributes).Returns(FileAttributes.Normal);
            var fileSystem = CreateFileSystem(path, dirInfo.Object, fileInfo.Object);
            FileSystemHelpers.Instance = fileSystem;

            var controller = CreateController(path);
            controller.Request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"this-will-not-match\""));
            controller.Request.Headers.IfMatch.Add(etag);

            // Act
            var response = await controller.DeleteItem();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            fileInfo.Verify(f => f.Delete());
        }

        [Theory]
        [MemberData("MapRouteToLocalPathData")]
        public void MapRouteToLocalPathTests(string requestUri, string expected)
        {
            FileSystemHelpers.Instance = new FileSystem();

            // in case of env variable not exist in certain target machine
            var expanded = System.Environment.ExpandEnvironmentVariables(expected);
            if (expanded.Contains("%"))
            {
                return;
            }

            if (requestUri.IndexOf("/LocalSiteRoot") > 0)
            {
                VfsSpecialFolders.LocalSiteRootPath = LocalSiteRootPath;
            }

            try
            {
                foreach (var suffixes in new[] { new[] { "", "" }, new[] { "/", "\\" } })
                {
                    // Arrange
                    var controller = CreateController(requestUri + suffixes[0], SiteRootPath);

                    // Act
                    string path = controller.GetLocalFilePath();

                    // Assert
                    Assert.Equal(expanded + suffixes[1], path);
                }
            }
            finally
            {
                VfsSpecialFolders.LocalSiteRootPath = null;
            }
        }

        public static IEnumerable<object[]> MapRouteToLocalPathData
        {
            get
            {
                yield return new object[] { "https://localhost/vfs", SiteRootPath };
                yield return new object[] { "https://localhost/vfs/LogFiles/kudu", SiteRootPath + @"\LogFiles\kudu" };
                
                yield return new object[] { "https://localhost/vfs/SystemDrive", "%SystemDrive%" };
                yield return new object[] { "https://localhost/vfs/SystemDrive/windows", @"%SystemDrive%\windows" };
                yield return new object[] { "https://localhost/vfs/SystemDrive/Program Files (x86)", @"%ProgramFiles(x86)%" };

                yield return new object[] { "https://localhost/vfs/LocalSiteRoot", LocalSiteRootPath };
                yield return new object[] { "https://localhost/vfs/LocalSiteRoot/Temp", LocalSiteRootPath + @"\Temp" };
            }
        }

        private static VfsController CreateController(string path, string rootPath = "/")
        {
            var env = new Mock<IEnvironment>();
            env.SetupGet(e => e.RootPath).Returns(rootPath);
            var request = new HttpRequestMessage();

            string routePath = path;
            Uri requestUri = null;
            if (Uri.TryCreate(path, UriKind.Absolute, out requestUri))
            {
                request.RequestUri = requestUri;

                // route path never starts with '/'
                routePath = requestUri.LocalPath.Replace("/vfs", String.Empty).TrimStart('/');
            }

            request.Properties[HttpPropertyKeys.HttpRouteDataKey] = new HttpRouteData(Mock.Of<IHttpRoute>(), new HttpRouteValueDictionary(new { path = routePath }));
            
            return new VfsController(Mock.Of<ITracer>(), env.Object)
            {
                Request = request
            };
        }

        private static IFileSystem CreateFileSystem(string path, DirectoryInfoBase dir, FileInfoBase file)
        {
            var directoryFactory = new Mock<IDirectoryInfoFactory>();
            directoryFactory.Setup(d => d.FromDirectoryName(path))
                            .Returns(dir);
            var fileInfoFactory = new Mock<IFileInfoFactory>();
            fileInfoFactory.Setup(f => f.FromFileName(path))
                           .Returns(file);

            var pathBase = new Mock<PathBase>();
            pathBase.Setup(p => p.GetFullPath(It.IsAny<string>()))
                    .Returns<string>(s => s);

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.SetupGet(f => f.DirectoryInfo).Returns(directoryFactory.Object);
            fileSystem.SetupGet(f => f.FileInfo).Returns(fileInfoFactory.Object);
            fileSystem.SetupGet(f => f.Path).Returns(pathBase.Object);

            FileSystemHelpers.Instance = fileSystem.Object;

            return fileSystem.Object;
        }

    }
}
