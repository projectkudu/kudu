using System;
using System.IO.Abstractions;
using Moq;
using Xunit;

namespace Kudu.Core.SSHKey.Test
{
    public class SSHKeyManagerFacts
    {
        [Fact]
        public void ConstructorThrowsIfEnvironmentIsNull()
        {
            // Arrange
            IEnvironment env = null;

            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new SSHKeyManager(env, fileSystem: null, traceFactory: null));
            Assert.Equal("environment", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsIfFileSystemIsNull()
        {
            // Arrange
            IEnvironment env = Mock.Of<IEnvironment>();
            IFileSystem fileSystem = null;

            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new SSHKeyManager(env, fileSystem, traceFactory: null));
            Assert.Equal("fileSystem", ex.ParamName);
        }

        [Fact]
        public void SetPrivateKeySetsByPassKeyCheckAndKeyIfFile()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no")).Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", "my super secret key")).Verifiable();

            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(sshPath)).Returns(true).Verifiable();
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey("my super secret key");
            
            // Assert
            fileBase.Verify();
        }

        [Fact]
        public void SetPrivateKeyCreatesSSHDirectoryIfItDoesNotExist()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no")).Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", "my super secret key")).Verifiable();

            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(sshPath)).Returns(false).Verifiable();
            directory.Setup(d => d.CreateDirectory(sshPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey("my super secret key");

            // Assert
            directory.Verify();
        }
    }
}
