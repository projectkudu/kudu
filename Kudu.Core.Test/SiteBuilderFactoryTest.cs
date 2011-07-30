using Kudu.Core.Deployment;
using Moq;
using Xunit;

namespace Kudu.Core.Test {
    public class SiteBuilderFactoryTest {
        [Fact]
        public void RequiresBuildCreatesMSBuildDeployer() {
            var environment = new Mock<IEnvironment>();
            environment.Setup(m => m.RequiresBuild).Returns(true);
            var builderFactory = new SiteBuilderFactory(environment.Object);

            ISiteBuilder builder = builderFactory.CreateBuilder();
            Assert.IsType(typeof(WapBuilder), builder);
        }

        [Fact]
        public void WhenRequiresBuildFalseCreatesBasicDeployer() {
            var environment = new Mock<IEnvironment>();
            environment.Setup(m => m.RequiresBuild).Returns(false);
            var builderFactory = new SiteBuilderFactory(environment.Object);

            ISiteBuilder builder = builderFactory.CreateBuilder();
            Assert.IsType(typeof(BasicBuilder), builder);
        }
    }
}
