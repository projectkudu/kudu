using System;
using System.IO;
using System.Reflection;
using System.Web;
using Kudu.Contracts.Settings;
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
            var handler = new VSOHandler(GetMockDeploymentSettingsMgr());

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Null(deploymentInfo.CommitId);
            Assert.Equal("VSO", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("VSO", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal(Resources.Vso_Synchronizing, deploymentInfo.TargetChangeset.Message);

            var repositoryUri = new Uri(deploymentInfo.RepositoryUrl);
            Assert.Equal("https", repositoryUri.Scheme);
            Assert.Equal("test01.vsoalm.tfsallin.net", repositoryUri.Host);
            Assert.Equal("/DefaultCollection/_git/testgit01", repositoryUri.AbsolutePath);
            Assert.Equal("this_is_vso_token:", repositoryUri.UserInfo);
        }

        [Fact]
        public void VSOHandlerUnknownPayload()
        {
            // Arrange
            var payload = new JObject();
            var httpRequest = new Mock<HttpRequestBase>();
            var handler = new VSOHandler(GetMockDeploymentSettingsMgr());

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

        private static IDeploymentSettingsManager GetMockDeploymentSettingsMgr()
        {
            var mockMgr = new Mock<IDeploymentSettingsManager>();
            mockMgr.Setup(m => m.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns(() =>
            {
                return @"https://test01.vsoalm.tfsallin.net/DefaultCollection/_git/testgit01";
            });

            return mockMgr.Object;
        }
    }
}