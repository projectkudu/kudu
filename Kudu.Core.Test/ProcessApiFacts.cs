using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Services.Performance;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class ProcessApiFacts
    {
        [Fact]
        public void MiniDumpDeleteOnDisposeTests()
        {
            // Arrange
            var path = @"x:\temp\minidump.dmp";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(file.Object);
            file.Setup(f => f.Exists(path))
                      .Returns(true);
            file.Setup(f => f.OpenRead(path))
                      .Returns(() => new MemoryStream());

            // Test
            using (var stream = ProcessController.MiniDumpStream.OpenRead(path, fileSystem.Object))
            {
            }

            // Assert
            file.Verify(f => f.Delete(path), Times.Once());
        }

        [Fact]
        public void MiniDumpDeleteOnCloseTests()
        {
            // Arrange
            var path = @"x:\temp\minidump.dmp";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(file.Object);
            file.Setup(f => f.Exists(path))
                      .Returns(true);
            file.Setup(f => f.OpenRead(path))
                      .Returns(() => new MemoryStream());

            // Test
            var stream = ProcessController.MiniDumpStream.OpenRead(path, fileSystem.Object);
            stream.Close();

            // Assert
            file.Verify(f => f.Delete(path), Times.Once());
        }
    }
}
