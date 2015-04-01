using System.Web;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class CodePlexHandlerFacts
    {
        [Theory]
        [InlineData("{ invalid: 'payload' }")]
        [InlineData("{ ref: '' }")]
        [InlineData(@"{""before"":""fc10b3aa5a9e39ac326489805bba5c577f04db85"",""after"":""840daf31f4f87cb5cafd295ef75de989095f415b"",""ref"":""refs/heads/master"",""repository"":{""name"":""Git Repo #1"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"",""clone_url"":""git@codebasehq.com:test/test-repositories/git1.git"",""clone_urls"":{""ssh"":""git@codebasehq.com:test/test-repositories/git1.git"",""git"":""git://codebasehq.com:test/test-repositories/git1.git"",""http"":""https://test.codebasehq.com/test-repositories/git1.git""},""project"":{""name"":""Test Repositories"",""url"":""http://test.codebasehq.com/projects/test-repositories"",""status"":""active""}},""user"":{""name"":""Dan Wentworth"",""username"":""dan"",""email"":""dan@atechmedia.com""},""commits"":[{""id"":""840daf31f4f87cb5cafd295ef75de989095f415b"",""message"":""Extra output for the rrrraaaagh"",""author"":{""name"":""Dan Wentworth"",""email"":""dan@atechmedia.com""},""timestamp"":""Mon, 18 Jul 2011 10:50:01 +0100"",""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1/commit/840daf31f4f87cb5cafd295ef75de989095f415b""}]}")]
        public void CodePlexHandlerIgnoresNonCodePlexPayloads(string payloadContent)
        {
            // Arrange
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Fact]
        public void CodePlexHandlerParsesGitPayload()
        {
            // Arrange
            string payloadContent = @"{ url: ""https://git01.codeplex.com/pranavkmgittest"", branch: ""master"", deployer: ""codeplex"", oldRef: ""3dc5fc28310a7906a9809f81fc5dc68aa681a1f8"", newRef: ""dcda9b7e70157a89423fd7862f2ee193586ca64f"", scmType: ""Git"" }";
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal("CodePlex", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("https://git01.codeplex.com/pranavkmgittest", deploymentInfo.RepositoryUrl);
            Assert.Equal("dcda9b7e70157a89423fd7862f2ee193586ca64f", deploymentInfo.TargetChangeset.Id);
        }

        [Fact]
        public void CodePlexHandlerNoOpsDeleteRequest()
        {
            // Arrange
            string payloadContent = @"{ url: ""https://git01.codeplex.com/pranavkmgittest"", branch: ""master"", deployer: ""codeplex"", oldRef: ""3dc5fc28310a7906a9809f81fc5dc68aa681a1f8"", newRef: ""0000000000000000000000000000000000000000"", scmType: ""Git"" }";
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void CodePlexHandlerNoOpsNonTargetBranchPayloads()
        {
            // Arrange
            string payloadContent = @"{ url: ""https://git01.codeplex.com/pranavkmgittest"", branch: ""test"", deployer: ""codeplex"", oldRef: ""3dc5fc28310a7906a9809f81fc5dc68aa681a1f8"", newRef: ""dcda9b7e70157a89423fd7862f2ee193586ca64f"", scmType: ""Git"" }";
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "prod", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void CodePlexHandlerParsesMercurialPayload()
        {
            // Arrange
            string payloadContent = @"{ url: ""https://hg01.codeplex.com/pranavkmmerctest"", branch: ""default"", deployer: ""codeplex"", oldRef: ""3dc5fc28310a7906a9809f81fc5dc68aa681a1f8"", newRef: ""dcda9b7e70157a89423fd7862f2ee193586ca64f"", scmType: ""Mercurial"" }";
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal("CodePlex", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Mercurial, deploymentInfo.RepositoryType);
            Assert.Equal("https://hg01.codeplex.com/pranavkmmerctest", deploymentInfo.RepositoryUrl);
            Assert.Equal("dcda9b7e70157a89423fd7862f2ee193586ca64f", deploymentInfo.TargetChangeset.Id);
        }

        [Fact]
        public void CodePlexHandlerNonOpsNonTargetBranchMercurialPayload()
        {
            // Arrange
            string payloadContent = @"{ url: ""https://hg01.codeplex.com/pranavkmmerctest"", branch: ""test"", deployer: ""codeplex"", oldRef: ""3dc5fc28310a7906a9809f81fc5dc68aa681a1f8"", newRef: ""dcda9b7e70157a89423fd7862f2ee193586ca64f"", scmType: ""Mercurial"" }";
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "production", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }
        
        [Theory]
        [InlineData(@"{ ""deployer"": ""codeplex"", ""newRef"": ""34bb60effd75"", ""oldRef"": ""000000000000"", ""scmType"": ""Git"", ""url"": ""https://git01.codeplex.com/mvc4application"" }")]
        [InlineData(@"{ ""deployer"": ""codeplex"", ""newRef"": ""8172e1304f9c"", ""oldRef"": ""000000000000"", ""scmType"": ""c"", ""url"": ""https://hg.codeplex.com/merctest2"" }")]
        public void CodePlexHandlerParsesInitialPayloadThatDoesNotHaveBranchInfo(string payloadContent)
        {
            // Arrange
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new CodePlexHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "production", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
        }
    }
}
