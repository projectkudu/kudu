using System.Web;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class CodeBaseHqFacts
    {
        [Fact]
        public void CodeBaseHqHandlerIgnoresNonCodeBaseHqPayloads()
        {
            // Arrange
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodebaseHqHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: null, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Theory]
        [InlineData("{ invalid: 'payload' }")]
        [InlineData("{ ref: '' }")]
        [InlineData(@"{ ""repository"":{ ""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1/commit/840daf31f4f87cb5cafd295ef75de989095f415b"" }, ref: """" }")]
        public void GitHubHandlerHandlerReturnsNoOpForMalformedPayloads(string payloadContent)
        {
            // Arrange
            var httpRequest = GetRequest();
            var handler = new CodebaseHqHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void CodeBaseHqHandlerProcessesPayload()
        {
            // Arrange
            string payloadContent = @"{""before"":""fc10b3aa5a9e39ac326489805bba5c577f04db85"",""after"":""840daf31f4f87cb5cafd295ef75de989095f415b"",""ref"":""refs/heads/master"",""repository"":{""name"":""Git Repo #1"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"",""clone_url"":""git@codebasehq.com:test/test-repositories/git1.git"",""clone_urls"":{""ssh"":""git@codebasehq.com:test/test-repositories/git1.git"",""git"":""git://codebasehq.com:test/test-repositories/git1.git"",""http"":""https://test.codebasehq.com/test-repositories/git1.git""},""project"":{""name"":""Test Repositories"",""url"":""http://test.codebasehq.com/projects/test-repositories"",""status"":""active""}},""user"":{""name"":""Dan Wentworth"",""username"":""dan"",""email"":""dan@atechmedia.com""},""commits"":[{""id"":""840daf31f4f87cb5cafd295ef75de989095f415b"",""message"":""Extra output for the rrrraaaagh"",""author"":{""name"":""Dan Wentworth"",""email"":""dan@atechmedia.com""},""timestamp"":""Mon, 18 Jul 2011 10:50:01 +0100"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1/commit/840daf31f4f87cb5cafd295ef75de989095f415b""}]}";
            var httpRequest = GetRequest();
            var handler = new CodebaseHqHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal("CodebaseHQ", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("https://test.codebasehq.com/test-repositories/git1.git", deploymentInfo.RepositoryUrl);
            Assert.Equal("840daf31f4f87cb5cafd295ef75de989095f415b", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Dan Wentworth", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("dan@atechmedia.com", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("Extra output for the rrrraaaagh", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void CodeBaseHqHandlerReturnsNoOpForDelete()
        {
            // Verifies delete scenario.
            // Arrange
            string payloadContent = @"{""before"":""fc10b3aa5a9e39ac326489805bba5c577f04db85"",""after"":""000000000000000000000000000000000"",""ref"":""refs/heads/master"",""repository"":{""name"":""Git Repo #1"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"",""clone_url"":""git@codebasehq.com:test/test-repositories/git1.git"",""clone_urls"":{""ssh"":""git@codebasehq.com:test/test-repositories/git1.git"",""git"":""git://codebasehq.com:test/test-repositories/git1.git"",""http"":""https://test.codebasehq.com/test-repositories/git1.git""},""project"":{""name"":""Test Repositories"",""url"":""http://test.codebasehq.com/projects/test-repositories"",""status"":""active""}},""user"":{""name"":""Dan Wentworth"",""username"":""dan"",""email"":""dan@atechmedia.com""},""commits"":[]}";
            var httpRequest = GetRequest();
            var handler = new CodebaseHqHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void CodeBaseHqHandlerReturnsNoOpForDeploymentsToNonTargetBranch()
        {
            // Arrange
            string payloadContent = @"{""before"":""fc10b3aa5a9e39ac326489805bba5c577f04db85"",""after"":""840daf31f4f87cb5cafd295ef75de989095f415b"",""ref"":""refs/heads/master"",""repository"":{""name"":""Git Repo #1"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"",""clone_url"":""git@codebasehq.com:test/test-repositories/git1.git"",""clone_urls"":{""ssh"":""git@codebasehq.com:test/test-repositories/git1.git"",""git"":""git://codebasehq.com:test/test-repositories/git1.git"",""http"":""https://test.codebasehq.com/test-repositories/git1.git""},""project"":{""name"":""Test Repositories"",""url"":""http://test.codebasehq.com/projects/test-repositories"",""status"":""active""}},""user"":{""name"":""Dan Wentworth"",""username"":""dan"",""email"":""dan@atechmedia.com""},""commits"":[{""id"":""840daf31f4f87cb5cafd295ef75de989095f415b"",""message"":""Extra output for the rrrraaaagh"",""author"":{""name"":""Dan Wentworth"",""email"":""dan@atechmedia.com""},""timestamp"":""Mon, 18 Jul 2011 10:50:01 +0100"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1/commit/840daf31f4f87cb5cafd295ef75de989095f415b""}]}";
            var httpRequest = GetRequest();
            var handler = new CodebaseHqHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "production", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
            Assert.Null(deploymentInfo);
        }

        private static HttpRequestBase GetRequest()
        {
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Codebasehq.com");
            return httpRequest.Object;
        }
    }
}
