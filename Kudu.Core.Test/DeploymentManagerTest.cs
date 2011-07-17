using Kudu.Core.Deployment;
using Moq;
using Xunit;

namespace Kudu.Core.Test {
    public class DeploymentManagerTest {
        [Fact]
        public void RequiesBuildCreatesMSBuildDeployer() {
            var environment = new Mock<IEnvironment>();
            environment.Setup(m => m.RequiresBuild).Returns(true);
            var deploymentManager = new DeploymentManager(environment.Object);

            IDeployer deployer = deploymentManager.CreateDeployer();
            Assert.IsType(typeof(MSBuildDeployer), deployer);
        }

        [Fact]
        public void WhenRequiesBuildFalseCreatesBasicDeployer() {
            var environment = new Mock<IEnvironment>();
            environment.Setup(m => m.RequiresBuild).Returns(false);
            var deploymentManager = new DeploymentManager(environment.Object);

            IDeployer deployer = deploymentManager.CreateDeployer();
            Assert.IsType(typeof(BasicDeployer), deployer);
        }
    }
}
