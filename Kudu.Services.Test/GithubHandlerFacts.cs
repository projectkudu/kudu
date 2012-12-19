using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class GithubHandlerFacts
    {
        //[Fact]
        //public void GitHubHandlerHandlerIgnoresNonBitbucketPayloads()
        //{
        //    // Arrange
        //    var httpRequest = new Mock<HttpRequestBase>();
        //    httpRequest.SetupGet(r => r.UserAgent).Returns("Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)");
        //    var bitbucketHandler = new GitHubHandler(Mock.Of<IGitServer>(), Mock.Of<ITraceFactory>(), Mock.Of<IEnvironment>(), new RepositoryConfiguration());

        //    // Act
        //    DeploymentInfo deploymentInfo;
        //    DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: null, targetBranch: null, deploymentInfo: out deploymentInfo);

        //    // Assert
        //    Assert.Equal(DeployAction.UnknownPayload, result);
        //}
    }
}
