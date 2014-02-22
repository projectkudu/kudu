using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Services.Diagnostics;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class RuntimeControllerFacts
    {
        private static readonly string _programFilesDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        private static readonly string _nodeDir = Path.Combine(_programFilesDir, "nodejs");

        [Fact]
        public void RuntimeControllerReturnsEmptyListIfDirectoryDoesNotExist()
        {
            // Arrange
            var nodeDir = new Mock<DirectoryInfoBase>();
            nodeDir.Setup(d => d.Exists).Returns(false);
            var directory = new Mock<IDirectoryInfoFactory>();
            directory.Setup(d => d.FromDirectoryName(_nodeDir)).Returns(nodeDir.Object);
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.DirectoryInfo).Returns(directory.Object);
            FileSystemHelpers.Instance = fileSystem.Object;
            var controller = new RuntimeController(Mock.Of<ITracer>());

            // Act
            var runtimeInfo = controller.GetRuntimeVersions();

            // Assert
            Assert.Empty(runtimeInfo.NodeVersions);
        }

        [Fact]
        public void RuntimeControllerReturnsNodeVersions()
        {
            // Arrange
            var nodeDir = new Mock<DirectoryInfoBase>();
            nodeDir.Setup(d => d.Exists).Returns(true);
            nodeDir.Setup(d => d.GetDirectories()).Returns(new[] { 
                CreateDirectory("0.8.19", CreateFile("npm.txt", "1.2.8")), 
                CreateDirectory("0.10.5", CreateFile("npm.txt", "1.3.11")), 
                CreateDirectory("0.10.18"), 
                CreateDirectory("node_modules"), 
                CreateDirectory("docs") 
            });
            var directoryInfo = new Mock<IDirectoryInfoFactory>();
            directoryInfo.Setup(d => d.FromDirectoryName(_nodeDir)).Returns(nodeDir.Object);
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.DirectoryInfo).Returns(directoryInfo.Object);
            FileSystemHelpers.Instance = fileSystem.Object;
            var controller = new RuntimeController(Mock.Of<ITracer>());

            // Act
            var nodeVersions = controller.GetRuntimeVersions().NodeVersions.ToList();

            // Assert
            Assert.Equal(new[] { "0.8.19", "0.10.5", "0.10.18" }, nodeVersions.Select(v => v["version"]));
            Assert.Equal(new[] { "1.2.8", "1.3.11", null }, nodeVersions.Select(v => v["npm"]));
        }

        private DirectoryInfoBase CreateDirectory(string name, params FileInfoBase[] files)
        {
            var dir = new Mock<DirectoryInfoBase>();
            dir.SetupGet(d => d.Name).Returns(name);
            dir.Setup(d => d.GetFiles(It.IsAny<string>())).Returns(files);
            return dir.Object;
        }

        private FileInfoBase CreateFile(string fileName, string content)
        {
            var file = new Mock<FileInfoBase>();
            file.SetupGet(f => f.Name).Returns(fileName);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            file.Setup(f => f.OpenRead()).Returns(memoryStream);
            return file.Object;
        }
    }
}
