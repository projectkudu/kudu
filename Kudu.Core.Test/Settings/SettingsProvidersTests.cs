using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Core.Settings;
using Moq;
using XmlSettings;
using Xunit;

namespace Kudu.Core.Test.Settings
{
    public class SettingsProvidersTests
    {
        private const string _configSection = "deployment";

        private static readonly IEnumerable<ISettingsProvider> EmptySettingsProviders = new List<ISettingsProvider>();

        [Fact]
        public void TestPerSiteSettingsProvider()
        {
            var key = "myKey";
            var value = "myValue";
            var keyValue = new KeyValuePair<string, string>(key, value);

            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.DeleteValue(_configSection, key)).Returns(true).Verifiable();
            settings.Setup(s => s.SetValue(_configSection, key, value)).Verifiable();
            settings.Setup(s => s.GetValue(_configSection, key)).Returns(value).Verifiable();
            settings.Setup(s => s.GetValues(_configSection)).Returns((new KeyValuePair<string, string>[] { keyValue }).ToList()).Verifiable();

            var perSiteSettingsProvider = new PerSiteSettingsProvider(settings.Object);

            perSiteSettingsProvider.SetValue(key, value);

            Assert.Equal(value, perSiteSettingsProvider.GetValue(key));

            var results = perSiteSettingsProvider.GetValues();
            Assert.Equal(1, results.Count());
            Assert.Equal(keyValue, results.ElementAt(0));

            perSiteSettingsProvider.DeleteValue(key);

            settings.Verify();
        }

        [Fact]
        public void TestEnvironmentSettingsProvider()
        {
            var key = "myKey";
            var value = "myValue";
            var keyValue = new KeyValuePair<string, string>(key, value);

            System.Environment.SetEnvironmentVariable("APPSETTING_" + key, value);

            var environmentSettingsProvider = new EnvironmentSettingsProvider();

            Assert.Equal(value, environmentSettingsProvider.GetValue(key));

            var results = environmentSettingsProvider.GetValues();
            Assert.Contains(keyValue, results);
        }

        [Fact]
        public void TestDefaultSettingsProvider()
        {
            var key = SettingsKeys.DeploymentBranch;
            var value = "master";
            var keyValue = new KeyValuePair<string, string>(key, value);

            var defaultSettingsProvider = new DefaultSettingsProvider();

            Assert.Equal(value, defaultSettingsProvider.GetValue(key));

            var results = defaultSettingsProvider.GetValues();
            Assert.Contains(keyValue, results);
        }

        [Fact]
        public void TestDeploymentSettingsProvider()
        {
            var key = "myKey";
            var value = "myValue";
            var keyValue = new KeyValuePair<string, string>(key, value);

            string dotDeploymentPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dotDeploymentPath);

            string dotDeploymentFilePath = Path.Combine(dotDeploymentPath, ".deployment");
            File.WriteAllText(
                dotDeploymentFilePath,
                @"
                  [config]
                  myKey = myValue
                  [moreConfig]
                  myKey2 = myValue2");

            try
            {
                var deploymentSettingsProvider = new DeploymentSettingsProvider(dotDeploymentPath);

                Assert.Equal(value, deploymentSettingsProvider.GetValue(key.ToUpperInvariant()));

                var results = deploymentSettingsProvider.GetValues();
                Assert.Equal(1, results.Count());
                Assert.Equal(keyValue, results.ElementAt(0));
            }
            finally
            {
                File.Delete(dotDeploymentFilePath);
                Directory.Delete(dotDeploymentPath);
            }
        }
    }
}
