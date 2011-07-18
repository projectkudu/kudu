using Kudu.Core.Deployment;
using Moq;
using Xunit;

namespace Kudu.Core.Test {
    public class DeployerFactoryTest {
        [Fact]
        public void RequiesBuildCreatesMSBuildDeployer() {
            var environment = new Mock<IEnvironment>();
            environment.Setup(m => m.RequiresBuild).Returns(true);
            var deployerFactory = new DeployerFactory(environment.Object);

            IDeployer deployer = deployerFactory.CreateDeployer();
            Assert.IsType(typeof(MSBuildDeployer), deployer);
        }

        [Fact]
        public void WhenRequiesBuildFalseCreatesBasicDeployer() {
            var environment = new Mock<IEnvironment>();
            environment.Setup(m => m.RequiresBuild).Returns(false);
            var deployerFactory = new DeployerFactory(environment.Object);

            IDeployer deployer = deployerFactory.CreateDeployer();
            Assert.IsType(typeof(BasicDeployer), deployer);
        }
    }
}
