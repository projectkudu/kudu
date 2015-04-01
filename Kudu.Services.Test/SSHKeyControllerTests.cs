using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.SSHKey;
using Kudu.Services.SSHKey;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class SSHKeyControllerTests
    {
        [Fact]
        public void GetPublicKeyDoesNotForceRecreatePublicKeyByDefault()
        {
            // Arrange
            var sshKeyManager = new Mock<ISSHKeyManager>(MockBehavior.Strict);
            string expected = "public-key";
            sshKeyManager.Setup(s => s.GetPublicKey(false))
                         .Returns(expected)
                         .Verifiable();
            var tracer = Mock.Of<ITracer>();
            var operationLock = new Mock<IOperationLock>();
            operationLock.Setup(l => l.Lock()).Returns(true);
            var controller = new SSHKeyController(tracer, sshKeyManager.Object, operationLock.Object);

            // Act
            string actual = controller.GetPublicKey();

            // Assert
            sshKeyManager.Verify();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        public void CreatePublicKeyForcesRecreateIfParameterIsSet(string ensurePublicKey)
        {
            // Arrange
            var sshKeyManager = new Mock<ISSHKeyManager>(MockBehavior.Strict);
            string expected = "public-key";
            sshKeyManager.Setup(s => s.GetPublicKey(true))
                         .Returns(expected)
                         .Verifiable();
            var tracer = Mock.Of<ITracer>();
            var operationLock = new Mock<IOperationLock>();
            operationLock.Setup(l => l.Lock()).Returns(true);
            var controller = new SSHKeyController(tracer, sshKeyManager.Object, operationLock.Object);

            // Act
            string actual = controller.GetPublicKey(ensurePublicKey);

            // Assert
            sshKeyManager.Verify();
            Assert.Equal(expected, actual);
        }
    }
}
