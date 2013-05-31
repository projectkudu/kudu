using System;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class RepositoryFactoryFacts
    {
        [Fact]
        public void EnsuringGitRepositoryThrowsIfDifferentRepositoryAlreadyExists()
        {
            foreach (RepositoryType repoType in Enum.GetValues(typeof(RepositoryType)))
            {
                foreach (RepositoryType currentType in Enum.GetValues(typeof(RepositoryType)))
                {
                    if (repoType == currentType)
                    {
                        continue;
                    }

                    // Arrange
                    var repoFactory = new Mock<RepositoryFactory>(Mock.Of<IEnvironment>(), Mock.Of<IDeploymentSettingsManager>(), Mock.Of<ITraceFactory>(), Mock.Of<HttpContextBase>()) { CallBase = true };
                    repoFactory.SetupGet(f => f.IsNullRepository)
                               .Returns(currentType == RepositoryType.None);
                    repoFactory.SetupGet(f => f.IsGitRepository)
                               .Returns(currentType == RepositoryType.Git);
                    repoFactory.SetupGet(f => f.IsHgRepository)
                               .Returns(currentType == RepositoryType.Mercurial);

                    // Act and Assert
                    var ex = Assert.Throws<InvalidOperationException>(() => repoFactory.Object.EnsureRepository(repoType));

                    Assert.Equal(String.Format("Expected a '{0}' repository but found a '{1}' repository at path ''.", repoType, currentType), ex.Message);
                }
            }
        }
    }
}
