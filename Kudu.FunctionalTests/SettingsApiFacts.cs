using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class SettingsApiFacts
    {
        [Fact]
        public void SetGetValue()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "SetValue";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.SettingsManager.SetValue("x", "1").Wait();
                    appManager.SettingsManager.SetValueLegacy("y", "2").Wait();

                    // Assert
                    string result = appManager.SettingsManager.GetValue("x").Result;
                    Assert.Equal("1", result);
                    result = appManager.SettingsManager.GetValue("y").Result;
                    Assert.Equal("2", result);

                });
            }
        }

        [Fact]
        public void SetGetDeleteValue()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "SetValue";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.SettingsManager.SetValue("x", "1").Wait();

                    // Assert
                    string result = appManager.SettingsManager.GetValue("x").Result;

                    Assert.Equal("1", result);

                    appManager.SettingsManager.Delete("x").Wait();

                    // Act
                    var ex = KuduAssert.ThrowsUnwrapped<HttpUnsuccessfulRequestException>(() => appManager.SettingsManager.GetValue("x").Wait());

                    // Assert
                    Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
                });
            }
        }

        [Fact]
        public void GetAllValues()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "SetValue";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.SettingsManager.SetValue("x", "1").Wait();
                    appManager.SettingsManager.SetValue("y", "2").Wait();
                    appManager.SettingsManager.SetValue("z", "3").Wait();

                    NameValueCollection results = appManager.SettingsManager.GetValues().Result;

                    // Assert
                    CheckSettings(results);
                });
            }
        }

        private static void CheckSettings(NameValueCollection results)
        {
            Assert.Equal("master", results["deployment_branch"]);
            Assert.Equal("", results[SettingsKeys.BuildArgs]);
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                Assert.Equal("1", results[SettingsKeys.TraceLevel]);
            }

            Assert.Equal("1", results["x"]);
            Assert.Equal("2", results["y"]);
            Assert.Equal("3", results["z"]);
        }

        [Fact]
        public void GetUndefinedKeyThrows404()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "GetNoValueThrows404";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    var ex = KuduAssert.ThrowsUnwrapped<HttpUnsuccessfulRequestException>(() => appManager.SettingsManager.GetValue("x").Wait());

                    // Assert
                    Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
                });
            }
        }

        [Fact]
        public async Task KuduVersionTest()
        {
            string appName = "KuduVersionTest";
            
            await ApplicationManager.RunAsync(appName, async appManager => 
            {
                // Arrange
                var id = Git.Id(Directory.GetCurrentDirectory());
                var client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.SettingsManager.Credentials);
                client.DefaultRequestHeaders.Accept.Clear();

                // Act
                string clientId = await client.GetStringAsync("commit.txt");

                // Assert
                 Assert.Equal(id, clientId.TrimEnd());
            });
        }
    }
}
