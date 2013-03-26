using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.SSHKey.Test
{
    public class SSHKeyManagerFacts
    {
        private const string _privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIGpAgEAAiEAuP52TyQ82vNoHmlxc3bFZnPBBguVXwp/LX4/IAWyEUUCASUCIFT/
Sx1xg76Lgt2KZI7/OBmHRuKr8nGmemgPyMdnd9MtAhEA9wWeggZekx/tUBIZAE2J
ZQIRAL+3tm2sgZSREmYsvm992mECEHgsP0YsnLZG4ib0DCmpLhUCEQCwLEbFpXAn
p+dkzyuJC91tAhEAqZQQlB/blelwf7hrrbEOfw==
-----END RSA PRIVATE KEY-----
";
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
            var fileBase = new Mock<FileBase>();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"))
                    .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", _privateKey))
                    .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa.pub", It.IsAny<string>()))
                    .Verifiable();

            var directory = new Mock<DirectoryBase>();
            directory.Setup(d => d.Exists(sshPath)).Returns(true).Verifiable();
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey(_privateKey);

            // Assert
            fileBase.Verify();
        }

        [Fact]
        public void SetPrivateKeyCreatesSSHDirectoryIfItDoesNotExist()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>();
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
            sshKeyManager.SetPrivateKey(_privateKey);

            // Assert
            directory.Verify();
        }

        [Fact]
        public void SetPrivateKeyAllowsRepeatedInvocation()
        {
            // Arrange
            string key1 = @"-----BEGIN RSA PRIVATE KEY-----
MIICWgIBAAKBgQCRLka+LfMP4esyl2Br+PEZ+QgQk8jDMa5rXVt+Idf/M1/2RVeW
E/odUygVPmOfpHa7crCq4aXJwYjRHxaGlidKSYBzd+JI0UBx6+1S8cGwUlH7riMU
ZAUyySIXpIkEpLTjYHbYsSXzblBh7olNm1vExaMaCh4m5D3tOs/kqCDbiwIBJQKB
gFZS3fSKBiUei9jkYthqgYUQnQLwFoHmMFuDni9SZMFBJE09/LoZt0+052aTzIhv
oIshmXpcp8QSNazGYGuzOfPnKtPwJikIZnEa36YEMorlrcG+sP25coqYWA3Q1US5
XsfyXmxBib5Enx3up/JwyFyceLwZzhjPoK/etWg5zTb9AkEA7xbPft5FHWAQ98oN
n9x1TXkAo/rRMNfUSSSCf4ZSFM0emNUvVx8SKEXcZhqWP1+VcDu9IE9PmITwvUe4
m7WxVQJBAJtzEPxmvrFiortOCzOQOiV6kkly9ZKPo/QjrHRWM1gldIF3ORpZZxhz
R45UQocITcKcTxt/3B0Td54/zjbX2V8CQCBPMMv0heFgAkr/oPntW/W2Z97O3f+u
dqIZsMUf/UEUzMiLgvAY9J2ok2e+Z1SsDUaEnQRdvqXoc48zNJ9rlIECQAyaoIMq
7N3zPaB78xIExnG91IJ+8VESkMDEn0ez9lNBTqK2o8PdvEBAssZZ2+FvYEA2L+15
EdjYEJ4g2V5khz8CQQDgIy+FkYp3GWHCOjdCXG88KxiCgDa/Tz42f/9ecS/XyzK+
AP1a+ov5cqO34vONhqb7iikB5o0X8Mm0hua4HlRu
-----END RSA PRIVATE KEY-----
";

            string publicKey = @"ssh-rsa AAAAB3NzaC1yc2EAAAABJQAAAIEAkS5Gvi3zD+HrMpdga/jxGfkIEJPIwzGua11bfiHX/zNf9kVXlhP6HVMoFT5jn6R2u3KwquGlycGI0R8WhpYnSkmAc3fiSNFAcevtUvHBsFJR+64jFGQFMskiF6SJBKS042B22LEl825QYe6JTZtbxMWjGgoeJuQ97TrP5Kgg24s=";

            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"));
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", It.IsAny<string>()));

            var directory = new Mock<DirectoryBase>();
            directory.Setup(d => d.Exists(sshPath)).Returns(true);
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey(key1);
            sshKeyManager.SetPrivateKey(_privateKey);

            // Assert
            fileBase.Verify(s => s.WriteAllText(sshPath + @"\id_rsa", It.IsAny<string>()), Times.Exactly(2));
            fileBase.Verify(s => s.WriteAllText(sshPath + @"\id_rsa.pub", publicKey));
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
            var actual = sshKeyManager.GetKey();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetSSHKeyCreatesKeyIfPublicAndPrivateKeyDoesNotAlreadyExist()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string keyOnDisk = null;
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(It.IsAny<string>()))
                    .Returns(false);
            fileBase.Setup(s => s.WriteAllText(sshPath + "\\id_rsa.pub", It.IsAny<string>()))
                   .Callback((string name, string value) => { keyOnDisk = value; })
                   .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + "\\id_rsa", It.IsAny<string>()))
                    .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"))
                    .Verifiable();

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetKey();

            // Assert
            fileBase.Verify();
            Assert.Equal(keyOnDisk, actual);
        }

        [Fact]
        public void GetSSHKeyReturnsPublicKeyIfItExists()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string publicKey = "this-is-my-public-key";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub"))
                    .Returns(true);
            fileBase.Setup(s => s.ReadAllText(sshPath + "\\id_rsa.pub"))
                    .Returns(publicKey);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetKey();

            // Assert
            Assert.Equal(publicKey, actual);
        }

        [Fact]
        public void GetSSHKeyDecodesPublicKeyFromPrivateKeyIfItExists()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string privateKey = @"-----BEGIN RSA PRIVATE KEY-----
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
            var publicKeyParams = new RSAParameters
            {
                Exponent = new byte[] { 1, 0, 1 },
                Modulus = Convert.FromBase64String("xqVRF/QIx/bAGbzkY+pVPAQ/BP1WPZ6hbWUZTryLS3OJ+rLJmWTe27xhoo/suTEUr6yOaUVeSxTg00Lvwsi1qsd1pMcZtjsB8CHkhdnsp7WxqGYIy0il9DdCMy6mv8Z80Jf0t8wahop6Klb5wRKJpxjIyIEIgxUwWuMpBuSwuH0=")
            };
            string publicKey = SSHEncoding.GetString(publicKeyParams);
            var fileBase = new Mock<FileBase>();
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa"))
                    .Returns(true);
            fileBase.Setup(s => s.ReadAllText(sshPath + "\\id_rsa"))
                    .Returns(privateKey);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetKey();

            // Assert
            Assert.Equal(publicKey, actual);
        }

        [Fact]
        public void GetSSHEncodedStringEncodesPublicKey()
        {
            // Arrange
            var publicKey = new RSAParameters
            {
                Exponent = new byte[] { 1, 0, 1 },
                Modulus = Convert.FromBase64String("u91Db5QwvFrtAFVuuDZQP/a4fZ12uVYYz2P8zit/A1u+o0d2ueN7orMcrkzmulfchYG64aBdjMN8JxKIeJTIbwXIq/LVLcQKq/BrPvu6HLhFFT7ZnrmHMbytHNnfJzG6MxjgIe0k2CHPsrCre20TPPZ+c3coW6PK3MHaS/cG80y1cS+FFU2HWKSlonRKgG4COcaRX8wdM1OLU2pph9tREG5frFLpqGwpdn9z4z8zEL/Wwgf26dsBSbFnU52DYjltjJKnV+B2eKiUd1u5izFFjuDyrLaRUZORF1sW4EO3jXlDpJdKtdQGqzJd6x0xCdUce0117sSAcHOilGi+n00y+Q==")
            };
            string expected = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC73UNvlDC8Wu0AVW64NlA/9rh9nXa5VhjPY/zOK38DW76jR3a543uisxyuTOa6V9yFgbrhoF2Mw3wnEoh4lMhvBcir8tUtxAqr8Gs++7ocuEUVPtmeuYcxvK0c2d8nMbozGOAh7STYIc+ysKt7bRM89n5zdyhbo8rcwdpL9wbzTLVxL4UVTYdYpKWidEqAbgI5xpFfzB0zU4tTammH21EQbl+sUumobCl2f3PjPzMQv9bCB/bp2wFJsWdTnYNiOW2MkqdX4HZ4qJR3W7mLMUWO4PKstpFRk5EXWxbgQ7eNeUOkl0q11AarMl3rHTEJ1Rx7TXXuxIBwc6KUaL6fTTL5";

            // Act
            string output = SSHEncoding.GetString(publicKey);

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


        public static IEnumerable<object[]> PrivateKey
        {
            get
            {
                yield return new object[]
                {
                    "yhP4LGiDCwMvd/Q+aQz++0RE6swY9aiVNxa+teB/JODRLMaMXHJU4ZLOrx3swBDzyCfQ2p7v/p0MBIWJEwsLU6JRHQLlAsOevpXw0oxSpqe/MQGNCyBdSylD56TZSFs/L5p+B1Or9cU+AG/XMGhOR80KrFYwIVPDZPff7ZOmaBM=",
                    new byte[] { 1, 0, 1 },
                    @"-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQDKE/gsaIMLAy939D5pDP77RETqzBj1qJU3Fr614H8k4NEsxoxc
clThks6vHezAEPPIJ9Danu/+nQwEhYkTCwtTolEdAuUCw56+lfDSjFKmp78xAY0L
IF1LKUPnpNlIWz8vmn4HU6v1xT4Ab9cwaE5HzQqsVjAhU8Nk99/tk6ZoEwIDAQAB
AoGBAIzky347CFMfT3N1aiZYl1edy+dhkm2FszQLucCZ3ExcK7vqW2cBmEkG0PCs
DqwDpdWCXU5wzqhZ200zxdTvOF9CS/GbQ3uYb1A/CxwPN9/FtwNenh0NU0Z2CFgp
+4QsU83hYGtokYauWKkz3Taz9w8i5uCu4KEBdaloBiJH7fZxAkEA/8eVbuuWbY/S
LBVZhDqeSbUB+VrG5aNZWGliKZZcbBx9FwdIXS1TI0YaN/S1Z9Sg9QqzBiyLRbhd
G0FgT/3HTwJBAMpAinwLq9iuRtuom3OShReZ1ireECEh4hOkVUPA9ymR0Nx2mIvz
2s3OTxgJrvmhWyHI2QVtJhsA5YUzYsCU4f0CQAXAtWmzPsTkETQQntzMfLbnrU2w
bvzHOcE1TZHl4dpEocOc1FHULSSD9R8BD/tv2tboELK42cENrnpodAQYjx0CQQDK
KAzDxF62LCxDLpqCwHcrieaZ3nA8zcNNYrqfCGeEM22SjzAW411WzNod6r/sYC3Y
7QqO8/RclV7U7vHMEIR5AkEAqqC8CgH3XWuBnXc935oGGvtc0hrKctSefnRBv/HI
n76PtB67TZo31AX3NBvZNenRyYaG8fgPUhsSU/yA6WydLQ==
-----END RSA PRIVATE KEY-----
"
                };

                yield return new object[] 
                { 
                    
                    "xqVRF/QIx/bAGbzkY+pVPAQ/BP1WPZ6hbWUZTryLS3OJ+rLJmWTe27xhoo/suTEUr6yOaUVeSxTg00Lvwsi1qsd1pMcZtjsB8CHkhdnsp7WxqGYIy0il9DdCMy6mv8Z80Jf0t8wahop6Klb5wRKJpxjIyIEIgxUwWuMpBuSwuH0=",
                    new byte[] { 1, 0, 1 },
                    @"-----BEGIN RSA PRIVATE KEY-----
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
"
                };

                yield return new object[]
                {
                    "wmC8M8FGoMGZMImAy828M2cjlHOvOwAL+1EjnXbMjfU6xNaakuPs5gGmKyU2jeTDKufxyZ2bujM62JTllxePQFqByOXPfF6w2dfqO8SMDa6rb2Fdx2R6APjlwVfRPNR7YR0yfyZ0ysWJkBtRegBm/p8utalIgesC8um+bOYtgdoFD1IMcpp+Mx7HOp47Jfqu9DUzxEqb6lpXWlihTCNqeEpr4hDBlL9nNraSMllxnkWWBExPvTikHtiYSD/MqOeAhVt7eoMPmTISayJoa6ikMD6hbZtA4xoSwhbv3ltONebf3GjLjrm29l+rbmIpU30s1Zzuq2BC0Slv8DRoUAkzx8WsjtCsiD3kqdzKJznz4gjGNBWPE5vqaBDttRJTBrVq0WdBGyXg/rp3YN4gfGFxszlCihUed9YaEEYdMTx+bUZ1uRW9AnTi5u2w4ZDuPDYKMR2Y7oJmXpWXGnzGxDTqUxyMjNf5QFq7IbN+EifXU05Gst9t0GrjHWDGJg/99T4t",
                    new byte[] { 1, 0, 1},
                    @"-----BEGIN RSA PRIVATE KEY-----
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
-----END RSA PRIVATE KEY-----
"
                };

                yield return new object[] 
                {
                    "1YXT32rLPUWv3eDmv/B9ZWDm6LFx+RN+PJSgVga0gyyCNzuEiuRXJYT/rB2XG8KycQ+df//0eYZ2rcUr+HE8UsqUQTCo3oH4L2xReD6uPCpOzrBhzowrokLUyUn4SpPWoO9ikBZaj0GfA5RRQWaQ84rqK0cGCm1tCwmEkAjZkz1Zvjbix9fI+F2WqhMZuIwby+L69jRu8gA4fv2QQRJyTLd3QD/Hci0SggPQfaMheLOgK0b1PqxmZjm7SZNyeNSR5W72GYT/YZUsc3bmp/0OyfPngoFnUqM1Vs08RL3e3mZgz+wIAiNs02JQCPqIuUl7cKjVkbkpvGohMRVWqfAZngKpM58siwBke7N2Ll6dc+hl9S86jSirdA8He4pjq4N1J2THlSETkAyj0mDVtLzQVHh8naG9Nuk67kg3J6lmyLHBXd8HMQ2jFDrOps96tcieOGNykZd0phF653BTvhdZihLgsqCqb5wuSm06AJX3rKWB8VqrE1aK13bTI3V743Oj3nT1uc9Qmd9ghLyaPSqzrL34ls141WQRb3GKIB3pZE6J0I30hwZpGlm/TLWH5Ph3ei2MCRFAdTF76UlXGUzGixqqa3y+ksl92VUoWDZL2yVtV2iqBXfUJjwn2CHMA5aqaenZuO0nnyvFsClnJtpRj110ndAnnZc5OHf8E+ubKVk=",
                    new byte[] { 1, 0, 1 },
                    @"-----BEGIN RSA PRIVATE KEY-----
MIIJKQIBAAKCAgEA1YXT32rLPUWv3eDmv/B9ZWDm6LFx+RN+PJSgVga0gyyCNzuE
iuRXJYT/rB2XG8KycQ+df//0eYZ2rcUr+HE8UsqUQTCo3oH4L2xReD6uPCpOzrBh
zowrokLUyUn4SpPWoO9ikBZaj0GfA5RRQWaQ84rqK0cGCm1tCwmEkAjZkz1Zvjbi
x9fI+F2WqhMZuIwby+L69jRu8gA4fv2QQRJyTLd3QD/Hci0SggPQfaMheLOgK0b1
PqxmZjm7SZNyeNSR5W72GYT/YZUsc3bmp/0OyfPngoFnUqM1Vs08RL3e3mZgz+wI
AiNs02JQCPqIuUl7cKjVkbkpvGohMRVWqfAZngKpM58siwBke7N2Ll6dc+hl9S86
jSirdA8He4pjq4N1J2THlSETkAyj0mDVtLzQVHh8naG9Nuk67kg3J6lmyLHBXd8H
MQ2jFDrOps96tcieOGNykZd0phF653BTvhdZihLgsqCqb5wuSm06AJX3rKWB8Vqr
E1aK13bTI3V743Oj3nT1uc9Qmd9ghLyaPSqzrL34ls141WQRb3GKIB3pZE6J0I30
hwZpGlm/TLWH5Ph3ei2MCRFAdTF76UlXGUzGixqqa3y+ksl92VUoWDZL2yVtV2iq
BXfUJjwn2CHMA5aqaenZuO0nnyvFsClnJtpRj110ndAnnZc5OHf8E+ubKVkCAwEA
AQKCAgBkRrdcA1FzcxjGwOpdVdnuFHYc7ciyyt7MIJi0De4UdICq4765Y8cxjaZs
9HCUzvjyc/zpshDkSavOq/ycbsF/uDer7ehApxUhYGNab0VwaAYet2MXl2ieiXhZ
F+4NSCTR69qEBJt/D7hX+/21EzAb0C9tJ6vEleNR/aRN6HoV1gghdrFGXSa6zWkG
cnXv34zmUbC+k51O9Z+StA5dIQag1MCiYdGO42//sz7k4gnEH8emy2o9hsWIWLCG
O0LVUC88asIU9grhjycTCtIELqoVWgBtn8wgWRmhrD0To3/ZPodU3mpcZrqjA1bH
ALHZIpNgM0opZ6YcIFN6M6VBpcrBOI3jeNWYMQX69UwkdeOx9xQgh8n54ONyjgVT
utvDw09ho9jup08FdI5M1c79X/gFRIqYi505QZqYaLaWxbtNfsonrikZLkPBUgkx
JOMmRclPcaCan6u8xQNylU/dEewPqfFlVRiDazJxmHfQnHNEbHROTPlsMRp2pGM6
DSUthnj1+xqD1fkU3rrdvYMShNkBSbX32zwxlkWVKnL33VewxXTkZeh6Zkon0TfS
FFOVQyr4fiXIZNlW0D2UDdSvzp95857epmrLZElfH06SA8//fFCITM1tDNd3wRZ5
rnC4e7+wxjhXVfK2QK4fyJr3/aIUKrrRwP0E9yoPxupVo6GxIQKCAQEA8je1SUB7
bHCfzGWsY4o2nZfnjt0qBM08B3hzd7AhEYTggnJbjTpKvstvL9acXkV0dMKbcdVq
wjLyrQwy9MXs3oxxyWELujeFKRkbcQEO7F8k64WPbDmXbPei/nlMFjlcfm1zICGG
TDhgZSkbMeP1+Hlc48YClL1uRvUddBCuTHZcnXm14Ui3bL1DYPU/lQEDuo/fHkCB
+bRiac/WqBkRV5GORA5nFZ0sPtV47JzLOyHEVpSO1Cb/832oLAZCyjDIxiodIlc0
1HHrIlJ+5HWBKQu0RMrsAzAzKk33cKfiJzCOGvJPzBKEpuA4T/5E/+sxaCp5BGwY
Q6juhGHUr79bPQKCAQEA4awh7/jtkgE74J4TZ+sZiV6M4wjiBtQXti4VVG5zDAV+
3kCklxQi9K6r2nHV2oNyCMBncbdqNrxxKBfprHn4zYl5rmdz902NZAhlEvLdABUL
jxTqFCeS2nnJ4u+irlKWGWPu2ar1n/Xw3GuEFs6nQeE28m42pdVDzOv+USQvplEn
HMynv/GJUVHJ3SBoRHBIzj+7WdBuizEWranMflnyVmz5HsGkHjTMQfFV69atBZzy
AAlwZuf0SgRoLdQTlXjTWv+uFA7RskQt32l+EnT1OTdlLeeicdun8LY0jy9rE2pA
GloqFEXcIBVj9sxF4Dz/LiyLq3yoE3FbKTNNrGsYTQKCAQEA7gbGtSST5Z3Lu15T
CUKipz3HBULb7voMur6oof7IkGHHCwn8ZA3btCFQs28wHQgeCDvR7AyxLARLLLkn
Phley9iyXRZsIuQ6jIeqyuMiWjCppHWM2urBnwi/+VkT52cZOPivwOyRAEgKmn7J
xb5iUnpZSVCl6qs5OqvX9N4LmwJZwzr+/FOsRUS8eQSpJfFoS6bkuOLll5CngZoI
NQrlWuukJccNkFTzTRAVFFiE8ygcvISi02M79XkPkavZaL6GHw71sHCIbxk/22u8
XSAH/GEPFudfBUcRkMorll60xJRXoa1rs3yjNSZ00E9sWR40YEwUvr7HHX5eXmOR
UeA3dQKCAQB9EanpVhtMHLTzoof8wtXvROBt/wFNaYQOqnGVznSiR/Vs9YSCWl2Z
H6kMsqQjq0+qu/9YjZ8m4L8RylbuCNc0CinO13T0rR1cQC7MFp8WqZMzZBLqwpfn
zzFtPQP6+rhHMBQyvEXOtj4b2tZk0Xju0QNjzmMo+w3NZ0kV7SkfUsCLfHzHqvRA
hkSK8af3rgcbj0Sk3Rg2uijobD9yEyV0coaKXiU3vGkrbrYAs4RGpRmVnaWW0pyX
3ONj6rJD16fDOgpfAWuEEbcep1eAoSM655GCpGpqEaN8i26LoGsGYo9OS4QgoisB
+Pji0Yk0YnnGPFfX3YlE5UDxj4ZPtTbNAoIBAQDZWp23QY0FuwFeIfBya/bRukDT
+GVVriOIn4CtNgau7ez7Dzm5JLrkBp0nShL781dDznHIhl5QC4GqJDd+xD/dLMo7
8VhiNZM9POND6Bfp9eZKED5BwDrl4SfO87fBZAgfJvTIPqDLDUZNEy8enWm2xqch
RgpHrCFY5Uztt+dRIb7yfuqUdhflr/ME5s2m7t611j2Sv5XM7WordAs+8wyYRFLo
SbgpyH1fDnyUM835nSfqcWr1vcZq655CLnPOQp6BtguHw8UY7IyM8cEfK6jHclrl
r1haBjoWUKxtwpjBbR49srwH1JF4nprfuNGq5tquHhF8ssip56i0RsNEilIo
-----END RSA PRIVATE KEY-----
"
                };
                
            }
        }

        [Theory]
        [PropertyData("PrivateKey")]
        public void ExtractPublicKeysExtractsKeysFromPEMEncodedPrivateKey(string expectedModulus, byte[] expectedExponent, string privateKey)
        {
            // Act
            RSAParameters output = PEMEncoding.ExtractPublicKey(privateKey);

            // Assert
            Assert.Equal(expectedModulus, Convert.ToBase64String(output.Modulus));
            Assert.Equal(expectedExponent, output.Exponent);
        }
    }
}
