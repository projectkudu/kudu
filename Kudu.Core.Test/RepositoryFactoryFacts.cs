using System;
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
                    if (repoType == currentType || currentType == RepositoryType.Zip)
                    {
                        continue;
                    }

                    var environment = new Mock<IEnvironment>();
                    environment.SetupGet(e => e.TempPath).Returns("x:\\temp");

                    // Arrange
                    var repoFactory = new Mock<RepositoryFactory>(
                        environment.Object,
                        Mock.Of<IDeploymentSettingsManager>(),
                        Mock.Of<ITraceFactory>()) { CallBase = true };
                    repoFactory.SetupGet(f => f.NoRepository)
                               .Returns(currentType == RepositoryType.None);
                    repoFactory.SetupGet(f => f.IsGitRepository)
                               .Returns(currentType == RepositoryType.Git);
                    repoFactory.SetupGet(f => f.IsHgRepository)
                               .Returns(currentType == RepositoryType.Mercurial);

                    // Act and Assert
                    if (repoType == RepositoryType.Zip)
                    {
                        // No assertion except that it doesn't throw
                        repoFactory.Object.EnsureRepository(repoType);
                    }
                    else
                    {
                        var ex = Assert.Throws<InvalidOperationException>(() => repoFactory.Object.EnsureRepository(repoType));
                        Assert.Equal(String.Format("Expected a '{0}' repository but found a '{1}' repository at path ''.", repoType, currentType), ex.Message);
                    }
                }
            }
        }
    }
}
