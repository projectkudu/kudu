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
using Kudu.Services.Editor;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Services.Test
{
    public class VfsControllerFacts
    {
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
            var controller = CreateController(path, fileSystem);

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

            var controller = CreateController(path, fileSystem);

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
            var controller = CreateController(path, fileSystem);

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

            var controller = CreateController(path, fileSystem);

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

            var controller = CreateController(path, fileSystem);
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
        [PropertyData("DeleteItemDeletesFileIfETagMatchesData")]
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

            var controller = CreateController(path, fileSystem);
            controller.Request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"this-will-not-match\""));
            controller.Request.Headers.IfMatch.Add(etag);

            // Act
            var response = await controller.DeleteItem();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            fileInfo.Verify(f => f.Delete());
        }

        private static VfsController CreateController(string path, IFileSystem fileBase)
        {
            var env = new Mock<IEnvironment>();
            env.SetupGet(e => e.RootPath).Returns("/");
            var request = new HttpRequestMessage();
            request.Properties[HttpPropertyKeys.HttpRouteDataKey] = new HttpRouteData(Mock.Of<IHttpRoute>(), new HttpRouteValueDictionary(new { path = path }));
            
            return new VfsController(Mock.Of<ITracer>(), env.Object, fileBase)
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

            return fileSystem.Object;
        }

    }
}
