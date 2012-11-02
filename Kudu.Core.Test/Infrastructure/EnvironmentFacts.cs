using System;
using System.IO.Abstractions;
using Kudu.Services.Web;
using Moq;
using Xunit;

using SystemEnvironment = System.Environment;

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
        public void ConstructorThrowsIfRepositoryPathResolverIsNull()
        {
            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new Environment(Mock.Of<IFileSystem>(), null, null, null, null, null, null, null, null, null));

            Assert.Equal("repositoryPath", ex.ParamName);
        }

        [Fact]
        public void RepositoryPathCreatesDirectoryIfItDoesNotExist()
        {
            // Arrange
            string repositoryPath = @"x:\deployment-repository";
            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(repositoryPath)).Returns(false);
            directory.Setup(d => d.CreateDirectory(repositoryPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(s => s.Directory).Returns(directory.Object);

            var environment = CreateEnvironment(mockFileSystem.Object, repositoryPath: repositoryPath);

            // Act
            string output = environment.RepositoryPath;

            // Assert
            Assert.Equal(repositoryPath, output);
            directory.Verify();
        }

        [Fact]
        public void WebRootPathCreatesDirectoryIfItDoesNotExist()
        {
            // Arrange
            string webRootPath = @"x:\webroot-path";
            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(webRootPath)).Returns(false);
            directory.Setup(d => d.CreateDirectory(webRootPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(s => s.Directory).Returns(directory.Object);

            var environment = CreateEnvironment(mockFileSystem.Object, webRootPath: webRootPath);

            // Act
            string output = environment.WebRootPath;

            // Assert
            Assert.Equal(webRootPath, output);
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

        [Fact]
        public void GitDisabledTest()
        {
            const string DisableGitKey = "kudu.disableGit";
            string previous = SystemEnvironment.GetEnvironmentVariable(DisableGitKey);
            Assert.Equal(AppSettings.DisableGit, previous == "1");

            try
            {
                SystemEnvironment.SetEnvironmentVariable(DisableGitKey, "1");
                Assert.Equal(true, AppSettings.DisableGit);

                SystemEnvironment.SetEnvironmentVariable(DisableGitKey, "0");
                Assert.Equal(false, AppSettings.DisableGit);

                SystemEnvironment.SetEnvironmentVariable(DisableGitKey, "TRUE");
                Assert.Equal(false, AppSettings.DisableGit);

                SystemEnvironment.SetEnvironmentVariable(DisableGitKey, String.Empty);
                Assert.Equal(false, AppSettings.DisableGit);

                SystemEnvironment.SetEnvironmentVariable(DisableGitKey, null);
                Assert.Equal(false, AppSettings.DisableGit);
            }
            finally
            {
                SystemEnvironment.SetEnvironmentVariable(DisableGitKey, previous);
            }
        }

        private static Environment CreateEnvironment(
            IFileSystem fileSystem = null,
            string rootPath = null,
            string siteRootPath = null,
            string tempPath = null,
            string repositoryPath = null,
            string webRootPath = null,
            string deployCachePath = null,
            string sshKeyPath = null,
            string nugetCachePath = null,
            string scriptPath = null)
        {
            fileSystem = fileSystem ?? Mock.Of<IFileSystem>();
            repositoryPath = repositoryPath ?? "";

            return new Environment(fileSystem,
                    rootPath,
                    siteRootPath,
                    tempPath,
                    repositoryPath,
                    webRootPath,
                    deployCachePath,
                    sshKeyPath,
                    nugetCachePath,
                    scriptPath);
        }
    }
}
