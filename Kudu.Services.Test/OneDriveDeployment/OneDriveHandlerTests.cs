using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Services.ServiceHookHandlers;
using Kudu.TestHarness.Xunit;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test.OneDriveDeployment
{
    [KuduXunitTestClass]
    public class OneDriveHandlerTests
    {
        [Fact]
        public void TryParseDeploymentInfoShouldReturnUnknownPayload()
        {
            var oneDriveHandler = new OneDriveHandler(Mock.Of<ITracer>(), Mock.Of<IDeploymentSettingsManager>(), Mock.Of<IEnvironment>());
            JObject payload = JObject.FromObject(new { });
            DeploymentInfo deploymentInfo = null;

            DeployAction result = oneDriveHandler.TryParseDeploymentInfo(null, payload, null, out deploymentInfo);
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Fact]
        public void TryParseDeploymentInfoShouldReturnProcessDeployment()
        {
            var oneDriveHandler = new OneDriveHandler(Mock.Of<ITracer>(), Mock.Of<IDeploymentSettingsManager>(), Mock.Of<IEnvironment>());
            JObject payload = JObject.FromObject(new { url = "https://api.onedrive.com", access_token = "one-drive-access-token" });
            DeploymentInfo deploymentInfo = null;

            DeployAction result = oneDriveHandler.TryParseDeploymentInfo(null, payload, null, out deploymentInfo);
            Assert.Equal(DeployAction.ProcessDeployment, result);
        }
    }
}
