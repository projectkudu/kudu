using System.Collections.Specialized;
using System.Net.Http;
using Kudu.Contracts.Settings;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
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
                    var ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.SettingsManager.GetValue("x").Wait());

                    // Assert
                    Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);
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

                    NameValueCollection resultsLegacy = appManager.SettingsManager.GetValuesLegacy().Result;
                    NameValueCollection results = appManager.SettingsManager.GetValues().Result;

                    // Assert
                    CheckSettings(resultsLegacy);
                    CheckSettings(results);
                });
            }
        }

        private static void CheckSettings(NameValueCollection results)
        {
            Assert.Equal("master", results[SettingsKeys.Branch]);
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
                    var ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.SettingsManager.GetValue("x").Wait());

                    // Assert
                    Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);
                });
            }
        }
    }
}
