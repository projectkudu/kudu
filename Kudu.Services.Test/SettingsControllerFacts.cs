using System.Net;
using System.Net.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Services.Settings;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class SettingsControllerFacts
    {
        [Fact]
        public void SettingsControllerConflict()
        {
            var settings = Mock.Of<IDeploymentSettingsManager>();
            var operationLock = new Mock<IOperationLock>();

            // setup
            operationLock.Setup(l => l.Lock())
                         .Returns(false);

            var controller = new SettingsController(settings, operationLock.Object)
            {
                Request = new HttpRequestMessage()
            };

            var response = controller.Set(new JObject());
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            response = controller.Delete("dummy");
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }
}