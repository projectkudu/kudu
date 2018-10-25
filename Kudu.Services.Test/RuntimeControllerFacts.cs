using System;
using System.Collections.Generic;
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
        private static readonly string _sharedRuntimeDir = Path.Combine(_programFilesDir, "dotnet", "shared");
        private static readonly string _dotNetCoreRuntimeDir = Path.Combine(_programFilesDir, "dotnet", "shared", "Microsoft.NETCore.App");
        private static readonly string _dotNetCoreSdkDir = Path.Combine(_programFilesDir, "dotnet", "sdk");

        [Fact]
        public void RuntimeControllerReturnsEmptyListIfDirectoryDoesNotExist()
        {
            // Arrange
            var nodeDir = new Mock<DirectoryInfoBase>();
            nodeDir.Setup(d => d.Exists).Returns(false);
            var directory = new Mock<IDirectoryInfoFactory>();
            directory.Setup(d => d.FromDirectoryName(_nodeDir)).Returns(nodeDir.Object);
            var fileSystem = new Mock<IFileSystem>();
            SetupFileSystemWithNoFiles(fileSystem);
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
            SetupFileSystemWithNoFiles(fileSystem);
            fileSystem.Setup(f => f.DirectoryInfo).Returns(directoryInfo.Object);
            FileSystemHelpers.Instance = fileSystem.Object;
            var controller = new RuntimeController(Mock.Of<ITracer>());

            // Act
            var nodeVersions = controller.GetRuntimeVersions().NodeVersions.ToList();

            // Assert
            Assert.Equal(new[] { "0.8.19", "0.10.5", "0.10.18" }, nodeVersions.Select(v => v["version"]));
            Assert.Equal(new[] { "1.2.8", "1.3.11", null }, nodeVersions.Select(v => v["npm"]));
        }

        [Fact]
        public void RuntimeControllerReturnsDotNetCoreVersions()
        {
            // Arrange
            var coreRuntimeDir = new Mock<DirectoryInfoBase>();
            coreRuntimeDir.Setup(d => d.Exists).Returns(true);
            coreRuntimeDir.Setup(d => d.GetDirectories()).Returns(new[] {
                CreateDirectory("2.2.7-alpha2"),
                CreateDirectory("2.2.7-alpha1"),
                CreateDirectory("2.2.7"),
                CreateDirectory("2.2.22"),
                CreateDirectory("1.0.12"),
                CreateDirectory("1.1.9"),
                CreateDirectory("1.1.12"),
                CreateDirectory("2.1.6"),
                CreateDirectory("ignore_me")
            });
            var coreSdkDir = new Mock<DirectoryInfoBase>();
            coreSdkDir.Setup(d => d.Exists).Returns(true);
            coreSdkDir.Setup(d => d.GetDirectories()).Returns(new[] {
                CreateDirectory("1.1.10"),
                CreateDirectory("2.1.403"),
                CreateDirectory("2.2.0-alpha")
            });
            var nonExistingDir = new Mock<DirectoryInfoBase>();
            nonExistingDir.Setup(d => d.Exists).Returns(false);
            var directoryInfo = new Mock<IDirectoryInfoFactory>();
            directoryInfo.Setup(d => d.FromDirectoryName(_dotNetCoreRuntimeDir)).Returns(coreRuntimeDir.Object);
            directoryInfo.Setup(d => d.FromDirectoryName(_dotNetCoreSdkDir)).Returns(coreSdkDir.Object);
            directoryInfo.Setup(d => d.FromDirectoryName(_nodeDir)).Returns(nonExistingDir.Object);
            var fileSystem = new Mock<IFileSystem>();
            SetupFileSystemWithNoFiles(fileSystem);
            fileSystem.Setup(f => f.DirectoryInfo).Returns(directoryInfo.Object);
            FileSystemHelpers.Instance = fileSystem.Object;
            var controller = new RuntimeController(Mock.Of<ITracer>());

            // Act
            dynamic coreVersions = controller.GetRuntimeVersions().DotNetCore32;
            var shared = (DotNetCoreSharedFrameworksInfo)coreVersions.shared;
            IEnumerable<string> sdk32Versions = coreVersions.sdk;

            // Assert
            Assert.Equal(new[] { "1.0.12", "1.1.9", "1.1.12", "2.1.6", "2.2.7-alpha1", "2.2.7-alpha2", "2.2.7", "2.2.22" }, shared.NetCoreApp);
            Assert.Equal(new[] { "1.1.10", "2.1.403", "2.2.0-alpha" }, sdk32Versions.ToArray());
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

        void SetupFileSystemWithNoFiles(Mock<IFileSystem> fileSystem)
        {
            var nonExistingFile = new Mock<FileInfoBase>();
            nonExistingFile.Setup(f => f.Exists).Returns(false);
            var fileInfo = new Mock<IFileInfoFactory>();
            fileInfo.Setup(f => f.FromFileName(It.IsAny<string>())).Returns(nonExistingFile.Object);
            fileSystem.Setup(f => f.FileInfo).Returns(fileInfo.Object);
        }
    }
}
