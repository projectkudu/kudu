using System.Collections.Specialized;
using System.Web;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class GithubHandlerFacts
    {
        [Fact]
        public void GitHubHandlerIgnoresNonGithubPayloads()
        {
            // Arrange
            var headers = new NameValueCollection();
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.Headers).Returns(headers);
            var handler = new GitHubHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: null, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Theory]
        [InlineData("{ invalid: 'payload' }")]
        [InlineData("{ ref: '' }")]
        [InlineData(@"{ ""repository"":{ ""url"":""https://github.com/KuduApps/PostCommitTest"" }, ref: """" }")]
        public void GitHubHandlerReturnsNoOpForMalformedPayloads(string payloadContent)
        {
            // Arrange
            var httpRequest = GetRequest();
            var handler = new GitHubHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void GitHubHandlerReturnsNoOpForPayloadsWithEmptyAfter()
        {
            // Verifies delete scenario.
            // Arrange
            string payloadContent = @"{""after"":""00000000000000000000000000000000"", ""repository"":{ ""url"":""https://github.com/KuduApps/PostCommitTest"" }, ref: ""refs/heads/master"", commits: [] }";
            var httpRequest = GetRequest();
            var handler = new GitHubHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void GitHubHandlerReturnsNoOpForPayloadsNotMatchingTargetBranch()
        {
            // Verifies delete scenario.
            // Arrange
            string payloadContent = @"{ ""repository"":{ ""url"":""https://github.com/KuduApps/PostCommitTest"" }, ref: ""refs/heads/not-master"", commits: [{""added"":[""Foo.txt""],""author"":{""email"":""prkrishn@hotmail.com"",""name"":""Pranav K"",""username"":""pranavkm""},""committer"":{""email"":""prkrishn@hotmail.com"",""name"":""Pranav K"",""username"":""pranavkm""},""distinct"":true,""id"":""f94996d67d6d5a060aaf2fcb72c333d0899549ab"",""message"":""Foo commit"",""modified"":[],""removed"":[],""timestamp"":""2012-12-17T14:32:20-08:00"",""url"":""https://github.com/KuduApps/PostCommitTest/commit/f94996d67d6d5a060aaf2fcb72c333d0899549ab""}] }";
            var httpRequest = GetRequest();
            var handler = new GitHubHandler();
            JObject payload = JObject.Parse(payloadContent);
            
            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }


        [Fact]
        public void GitHubHandlerProcessesPayloadWithMultipleCommits()
        {
            // Verifies delete scenario.
            // Arrange
            string payloadContent = @"{""after"":""f94996d67d6d5a060aaf2fcb72c333d0899549ab"",""before"":""0000000000000000000000000000000000000000"",""commits"":[{""added"":[],""author"":{""email"":""kirthik@microsoft.com"",""name"":""Kirthi Krishnamraju"",""username"":""kirthik""},""committer"":{""email"":""kirthik@microsoft.com"",""name"":""Kirthi Krishnamraju"",""username"":""kirthik""},""distinct"":true,""id"":""18ceab5cda610374b45f6496c88615b1213a7bd8"",""message"":""in foo"",""modified"":[""MvcApplication1/Controllers/HomeController.cs""],""removed"":[],""timestamp"":""2012-08-30T17:36:29-07:00"",""url"":""https://github.com/KuduApps/PostCommitTest/commit/18ceab5cda610374b45f6496c88615b1213a7bd8""},{""added"":[""Foo.txt""],""author"":{""email"":""prkrishn@hotmail.com"",""name"":""Pranav K"",""username"":""pranavkm""},""committer"":{""email"":""prkrishn@hotmail.com"",""name"":""Pranav K"",""username"":""pranavkm""},""distinct"":true,""id"":""f94996d67d6d5a060aaf2fcb72c333d0899549ab"",""message"":""Foo commit"",""modified"":[],""removed"":[],""timestamp"":""2012-12-17T14:32:20-08:00"",""url"":""https://github.com/KuduApps/PostCommitTest/commit/f94996d67d6d5a060aaf2fcb72c333d0899549ab""}],""compare"":""https://github.com/KuduApps/PostCommitTest/compare/18ceab5cda61^...f94996d67d6d"",""created"":true,""deleted"":false,""forced"":true,""pusher"":{""email"":""prkrishn@hotmail.com"",""name"":""pranavkm""},""ref"":""refs/heads/master"",""repository"":{""private"":false,""url"":""https://github.com/KuduApps/PostCommitTest""}}";
            var httpRequest = GetRequest();
            var handler = new GitHubHandler();
            JObject payload = JObject.Parse(payloadContent);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal("GitHub", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("https://github.com/KuduApps/PostCommitTest", deploymentInfo.RepositoryUrl);
            Assert.Equal("f94996d67d6d5a060aaf2fcb72c333d0899549ab", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Pranav K", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("prkrishn@hotmail.com", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("Foo commit", deploymentInfo.TargetChangeset.Message);
        }

        private static HttpRequestBase GetRequest()
        {
            var headers = new NameValueCollection();
            headers.Add("X-Github-Event", "push");
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.Headers).Returns(headers);
            return httpRequest.Object;
        }
    }
}
