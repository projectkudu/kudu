using System;
using System.IO.Abstractions;
using System.Security.Cryptography;
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
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(false).Verifiable();
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
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(false).Verifiable();
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

        [Fact]
        public void SetPrivateKeyAllowsRepeatedInvocationIfNoPublicKeyIsPresent()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(false);
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"));
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", It.IsAny<string>()));

            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(sshPath)).Returns(true);
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey("key 1");
            sshKeyManager.SetPrivateKey("key 2");

            // Assert
            fileBase.Verify(s => s.WriteAllText(sshPath + @"\id_rsa", It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public void SetPrivateKeyThrowsIfAPublicKeyAlreadyExistsOnFileSystem()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(true).Verifiable();

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act and Assert
            var ex = Assert.Throws<InvalidOperationException>(() => sshKeyManager.SetPrivateKey("my super secret key"));
            Assert.Equal("Cannot set key. A key pair already exists on disk. To generate a new key set 'forceCreate' to true.", ex.Message);
        }

        [Fact]
        public void GetSSHKeyReturnsExistingKeyIfPresentOnDisk()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string expected = "my-public-key";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(true);
            fileBase.Setup(s => s.ReadAllText(sshPath + "\\id_rsa.pub")).Returns(expected);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetOrCreateKey(forceCreate: false);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetSSHKeyCreatesKeyIfForceCreateIsSet()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string keyOnDisk = null;
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.WriteAllText(sshPath + "\\id_rsa.pub", It.IsAny<string>()))
                   .Callback((string name, string value) => { keyOnDisk = value; }).Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + "\\id_rsa", It.IsAny<string>())).Verifiable();

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetOrCreateKey(forceCreate: true);

            // Assert
            fileBase.Verify();
            Assert.Equal(keyOnDisk, actual);
        }

        [Fact]
        public void GetSSHEncodedStringEncodesPublicKey()
        {
            // Arrange
            var privateKey = new RSAParameters
            {
                Exponent = new byte[] { 1, 0, 1 },
                Modulus = Convert.FromBase64String("xqVRF/QIx/bAGbzkY+pVPAQ/BP1WPZ6hbWUZTryLS3OJ+rLJmWTe27xhoo/suTEUr6yOaUVeSxTg00Lvwsi1qsd1pMcZtjsB8CHkhdnsp7WxqGYIy0il9DdCMy6mv8Z80Jf0t8wahop6Klb5wRKJpxjIyIEIgxUwWuMpBuSwuH0="),
            };
            string expected = "ssh-rsa AAAAgQDGpVEX9AjH9sAZvORj6lU8BD8E/VY9nqFtZRlOvItLc4n6ssmZZN7bvGGij+y5MRSvrI5pRV5LFODTQu/CyLWqx3Wkxxm2OwHwIeSF2eyntbGoZgjLSKX0N0IzLqa/xnzQl/S3zBqGinoqVvnBEomnGMjIgQiDFTBa4ykG5LC4fQAAAAAAAAAAAAAAAAAAAAAAAA==";

            // Act
            string output = SSHEncoding.GetString(privateKey);

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void GetPEMEncodedStringEncodesPrivateKey()
        {
            // Arrange
            var privateKey = new RSAParameters
            {
                D = Convert.FromBase64String("KaWHNupm3OWSqNK9X2VFsWuCet1SM2EKnxDPGX7WBV+X0gOh2JMZViBMp/RcwQbVO2+F+/QbLMqXyDMEaWYDEAhqBeF2VPKuoHPWyxpiOxYUiqgskB7FH4QWdml2eAZp5DGL1f98JMGpb2NVqe2+Dxg92Yf7aKwjlf8OGVrKJVE="),
                DP = Convert.FromBase64String("VxvOmWBK86gMUNGMY/3Iy/n4t+XsJdbEYSIBuXuzsF2CdeMh77YJIDuLktg48IZgdWgt20GBMhcrf+XL0elGkw=="),
                DQ = Convert.FromBase64String("lVQxek9GPkVJq1V/k6+Jvfsippt9ulp+G1S6Dt20jvjgXlyTOmDYV7/f0ZIvc04gjMGtUsPYdKyYt3JFvVFNYQ=="),
                Exponent = new byte[] { 1, 0, 1 },
                InverseQ = Convert.FromBase64String("2dtFkIGZON5RWcq5dAOG42njvbzrRJUOuh2rHbq9SfTs4pKn5kIPebEQOqFxDWs005p067miumRGhLZ3boxJew=="),
                Modulus = Convert.FromBase64String("xqVRF/QIx/bAGbzkY+pVPAQ/BP1WPZ6hbWUZTryLS3OJ+rLJmWTe27xhoo/suTEUr6yOaUVeSxTg00Lvwsi1qsd1pMcZtjsB8CHkhdnsp7WxqGYIy0il9DdCMy6mv8Z80Jf0t8wahop6Klb5wRKJpxjIyIEIgxUwWuMpBuSwuH0="),
                P = Convert.FromBase64String("4w4EblF6UB7KE3dVFZOxyAIVixRFuOZf7niPjsCr0/x8SvQ3yMYa14OTsL472yKTvDIrECbmH8Adju8/hWHM/w=="),
                Q = Convert.FromBase64String("3/gqdG19aGYIuKj7Fe38tGGt5S4NEFvR0i2up+5lr9aoTZj9moFICqP+Ojkvvr5n1RSueTX3JQ5rorNmLDEugw==")
            };
            string expected = @"-----BEGIN RSA PRIVATE KEY-----
MIICXQIBAAKBgQDGpVEX9AjH9sAZvORj6lU8BD8E/VY9nqFtZRlOvItLc4n6ssmZ
ZN7bvGGij+y5MRSvrI5pRV5LFODTQu/CyLWqx3Wkxxm2OwHwIeSF2eyntbGoZgjL
SKX0N0IzLqa/xnzQl/S3zBqGinoqVvnBEomnGMjIgQiDFTBa4ykG5LC4fQIDAQAB
AoGAKaWHNupm3OWSqNK9X2VFsWuCet1SM2EKnxDPGX7WBV+X0gOh2JMZViBMp/Rc
wQbVO2+F+/QbLMqXyDMEaWYDEAhqBeF2VPKuoHPWyxpiOxYUiqgskB7FH4QWdml2
eAZp5DGL1f98JMGpb2NVqe2+Dxg92Yf7aKwjlf8OGVrKJVECQQDjDgRuUXpQHsoT
d1UVk7HIAhWLFEW45l/ueI+OwKvT/HxK9DfIxhrXg5OwvjvbIpO8MisQJuYfwB2O
7z+FYcz/AkEA3/gqdG19aGYIuKj7Fe38tGGt5S4NEFvR0i2up+5lr9aoTZj9moFI
CqP+Ojkvvr5n1RSueTX3JQ5rorNmLDEugwJAVxvOmWBK86gMUNGMY/3Iy/n4t+Xs
JdbEYSIBuXuzsF2CdeMh77YJIDuLktg48IZgdWgt20GBMhcrf+XL0elGkwJBAJVU
MXpPRj5FSatVf5Ovib37IqabfbpafhtUug7dtI744F5ckzpg2Fe/39GSL3NOIIzB
rVLD2HSsmLdyRb1RTWECQQDZ20WQgZk43lFZyrl0A4bjaeO9vOtElQ66Hasdur1J
9OzikqfmQg95sRA6oXENazTTmnTruaK6ZEaEtndujEl7
-----END RSA PRIVATE KEY-----
";

            // Act
            string output = PEMEncoding.GetString(privateKey);

            // Assert
            Assert.Equal(expected, output);
        }
    }
}
