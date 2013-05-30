using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class FileSystemHelpersTest
    {
        [Fact]
        public void EnsureDirectoryCreatesDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(false);

            string path = FileSystemHelpers.EnsureDirectory(fileSystem.Object, "foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Once());
        }

        [Fact]
        public void EnsureDirectoryDoesNotCreateDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(true);

            string path = FileSystemHelpers.EnsureDirectory(fileSystem.Object, "foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Never());
        }
    }
}
