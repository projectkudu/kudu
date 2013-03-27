using System;
using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.SSHKey;
using Kudu.Core.Tracing;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class RepositoryFactoryFacts
    {
        [Theory]
        [InlineData(RepositoryType.Git, false, true, "Expected a 'Git' repository but found a 'Mercurial' repository at path ''.")]
        [InlineData(RepositoryType.Mercurial, true, false, "Expected a 'Mercurial' repository but found a 'Git' repository at path ''.")]
        public void EnsuringGitRepositoryThrowsIfDifferentRepositoryAlreadyExists(RepositoryType repoType, bool isGit, bool isMercurial, string message)
        {
            // Arrange
            var repoFactory = new Mock<RepositoryFactory>(Mock.Of<IEnvironment>(), Mock.Of<IDeploymentSettingsManager>(), Mock.Of<ITraceFactory>()) { CallBase = true };
            repoFactory.SetupGet(f => f.IsGitRepository)
                       .Returns(isGit);
            repoFactory.SetupGet(f => f.IsHgRepository)
                       .Returns(isMercurial);
            
            // Act and Assert
            var ex = Assert.Throws<InvalidOperationException>(() => repoFactory.Object.EnsureRepository(repoType));

            Assert.Equal(message, ex.Message);
        }
    }
}
