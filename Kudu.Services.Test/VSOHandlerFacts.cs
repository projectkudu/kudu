using System;
using System.IO;
using System.Reflection;
using System.Web;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class VSOHandlerFacts
    {
        [Fact]
        public void VSOHandlerBasic()
        {
            // Arrange
            var payload = GetVSOPayload();
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new VSOHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal("this_is_latest_commit", deploymentInfo.CommitId);
            Assert.Equal("this_is_latest_commit", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("John Smith", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("test01@hotmail.com", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("commit message", deploymentInfo.TargetChangeset.Message);
            Assert.Equal(DateTime.Parse("2015-05-08T00:20:59Z"), deploymentInfo.TargetChangeset.Timestamp);

            var repositoryUri = new Uri(deploymentInfo.RepositoryUrl);
            Assert.Equal("https", repositoryUri.Scheme);
            Assert.Equal("test01.vsoalm.tfsallin.net", repositoryUri.Host);
            Assert.Equal("/DefaultCollection/_git/testgit01", repositoryUri.AbsolutePath);
            Assert.Equal("this_is_vso_token", repositoryUri.UserInfo);
        }

        [Fact]
        public void VSOHandlerMismatchBranch()
        {
            // Arrange
            var payload = GetVSOPayload();
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new VSOHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "foo", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
        }

        [Fact]
        public void VSOHandlerUnknownPayload()
        {
            // Arrange
            var payload = new JObject();
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new VSOHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        private static JObject GetVSOPayload()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var reader = new StreamReader(assembly.GetManifestResourceStream("Kudu.Services.Test.vsopayload.json")))
            {
                return (JObject)JToken.ReadFrom(new JsonTextReader(reader));
            }
        }
    }
}