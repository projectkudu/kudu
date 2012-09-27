using System;
using System.IO.Abstractions;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class EnvironmentFacts
    {
        [Fact]
        public void ConstructorThrowsIfFileSystemIsNull()
        {
            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new Environment(null, null, null, null, null, null, null, null, null, null));

            Assert.Equal("fileSystem", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsIfDeploymentRepositoryPathResolverIsNull()
        {
            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new Environment(Mock.Of<IFileSystem>(), null, null, null, null, null, null, null, null, null));

            Assert.Equal("deploymentRepositoryPathResolver", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsIfRepositoryPathResolverIsNull()
        {
            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new Environment(Mock.Of<IFileSystem>(), null, null, () => "", null, null, null, null, null, null));

            Assert.Equal("repositoryPathResolver", ex.ParamName);
        }

        [Fact]
        public void DeploymentRepositoryPathCreatesDirectoryIfItDoesNotExist()
        {
            // Arrange
            string deploymentRepositoryPath = @"x:\deployment-repository";
            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(deploymentRepositoryPath)).Returns(false);
            directory.Setup(d => d.CreateDirectory(deploymentRepositoryPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(s => s.Directory).Returns(directory.Object);

            var environment = CreateEnvironment(mockFileSystem.Object, deploymentRepositoryPathResolver: () => deploymentRepositoryPath);

            // Act
            string output = environment.DeploymentRepositoryPath;

            // Assert
            Assert.Equal(deploymentRepositoryPath, output);
            directory.Verify();
        }

        [Fact]
        public void DeploymentTargetPathCreatesDirectoryIfItDoesNotExist()
        {
            // Arrange
            string deployPath = @"x:\deployment-path";
            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(deployPath)).Returns(false);
            directory.Setup(d => d.CreateDirectory(deployPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(s => s.Directory).Returns(directory.Object);

            var environment = CreateEnvironment(mockFileSystem.Object, deployPath: deployPath);

            // Act
            string output = environment.DeploymentTargetPath;

            // Assert
            Assert.Equal(deployPath, output);
            directory.Verify();
        }

        [Fact]
        public void DeploymentCachePathCreatesDirectoryIfItDoesNotExist()
        {
            // Arrange
            string deployCachePath = @"x:\deployment-cache";
            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(deployCachePath)).Returns(false);
            directory.Setup(d => d.CreateDirectory(deployCachePath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(s => s.Directory).Returns(directory.Object);

            var environment = CreateEnvironment(mockFileSystem.Object, deployCachePath: deployCachePath);

            // Act
            string output = environment.DeploymentCachePath;

            // Assert
            Assert.Equal(deployCachePath, output);
            directory.Verify();
        }

        [Fact]
        public void SSHKeyPathCreatesDirectoryIfItDoesNotExist()
        {
            // Arrange
            string sshPath = @"x:\ssh-path";
            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(sshPath)).Returns(false);
            directory.Setup(d => d.CreateDirectory(sshPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(s => s.Directory).Returns(directory.Object);

            var environment = CreateEnvironment(mockFileSystem.Object, sshKeyPath: sshPath);

            // Act
            string output = environment.SSHKeyPath;

            // Assert
            Assert.Equal(sshPath, output);
            directory.Verify();
        }

        private static Environment CreateEnvironment(
            IFileSystem fileSystem = null,
            string applicationRootPath = null,
            string tempPath = null,
            Func<string> deploymentRepositoryPathResolver = null,
            Func<string> repositoryPathResolver = null,
            string deployPath = null,
            string deployCachePath = null,
            string sshKeyPath = null,
            string nugetCachePath = null,
            string scriptPath = null)
        {
            fileSystem = fileSystem ?? Mock.Of<IFileSystem>();
            deploymentRepositoryPathResolver = deploymentRepositoryPathResolver ?? (() => "");
            repositoryPathResolver = repositoryPathResolver ?? (() => "");

            return new Environment(fileSystem,
                    applicationRootPath,
                    tempPath,
                    deploymentRepositoryPathResolver,
                    repositoryPathResolver,
                    deployPath,
                    deployCachePath,
                    sshKeyPath,
                    nugetCachePath,
                    scriptPath);
        }
    }
}
