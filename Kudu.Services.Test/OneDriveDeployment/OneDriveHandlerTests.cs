using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Services.FetchHelpers;
using Kudu.Services.ServiceHookHandlers;
using Kudu.TestHarness.Xunit;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.Test.OneDriveDeployment
{
    [KuduXunitTestClass]
    public class OneDriveHandlerTests
    {
        [Fact]
        public void TryParseDeploymentInfoShouldReturnUnknownPayload()
        {
            var oneDriveHandler = new OneDriveHandler(Mock.Of<ITracer>(), Mock.Of<IDeploymentStatusManager>(), Mock.Of<IDeploymentSettingsManager>(), Mock.Of<IEnvironment>(), Mock.Of<IRepositoryFactory>());
            JObject payload = JObject.FromObject(new { });
            DeploymentInfoBase deploymentInfo = null;

            DeployAction result = oneDriveHandler.TryParseDeploymentInfo(null, payload, null, out deploymentInfo);
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Fact]
        public void TryParseDeploymentInfoShouldReturnProcessDeployment()
        {
            var oneDriveHandler = new OneDriveHandler(Mock.Of<ITracer>(), Mock.Of<IDeploymentStatusManager>(), Mock.Of<IDeploymentSettingsManager>(), Mock.Of<IEnvironment>(), Mock.Of<IRepositoryFactory>());
            JObject payload = JObject.FromObject(new { RepositoryUrl = "https://api.onedrive.com", AccessToken = "one-drive-access-token" });
            DeploymentInfoBase deploymentInfo = null;

            DeployAction result = oneDriveHandler.TryParseDeploymentInfo(null, payload, null, out deploymentInfo);
            Assert.Equal(DeployAction.ProcessDeployment, result);
        }
    }
}
