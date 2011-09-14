using Kudu.Core.Deployment;
using Moq;
using Xunit;

namespace Kudu.Core.Test {
    public class DeploymentManagerTest {
        private const string SettingConfig = @"<configuration>
  <appSettings>
    <add key=""key"" value=""val"" />
  </appSettings>
  <connectionStrings>
    <add name=""foo"" connectionString=""dev"" providerName=""provider"" />
    <add name=""bar"" connectionString=""dev-bar"" providerName=""provider"" />
  </connectionStrings>
</configuration>";

        [Fact]
        public void TransformWithNoSettingsDoesNothing() {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            var document = DeploymentManager.Transform(mockSettingProvider.Object, SettingConfig);

            Assert.Equal(SettingConfig.Trim(), document.ToString().Trim());
        }

        [Fact]
        public void TransformWithExistingAppSettingReplacesSetting() {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetAppSettings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "key",
                       Value = "deployment"
                   }
            });

            var document = DeploymentManager.Transform(mockSettingProvider.Object, SettingConfig);

            Assert.Equal(@"<configuration>
  <appSettings>
    <add key=""key"" value=""deployment"" />
  </appSettings>
  <connectionStrings>
    <add name=""foo"" connectionString=""dev"" providerName=""provider"" />
    <add name=""bar"" connectionString=""dev-bar"" providerName=""provider"" />
  </connectionStrings>
</configuration>", document.ToString().Trim());
        }

        [Fact]
        public void TransformWithNewAppSettingAddsSetting() {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetAppSettings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "kudu",
                       Value = "foo"
                   }
            });

            var document = DeploymentManager.Transform(mockSettingProvider.Object, SettingConfig);

            Assert.Equal(@"<configuration>
  <appSettings>
    <add key=""key"" value=""val"" />
    <add key=""kudu"" value=""foo"" />
  </appSettings>
  <connectionStrings>
    <add name=""foo"" connectionString=""dev"" providerName=""provider"" />
    <add name=""bar"" connectionString=""dev-bar"" providerName=""provider"" />
  </connectionStrings>
</configuration>", document.ToString().Trim());
        }

        [Fact]
        public void TransformWithConnectionStringSettingReplacesConnectionString() {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetConnectionStrings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "foo",
                       Value = "production"
                   }
            });

            var document = DeploymentManager.Transform(mockSettingProvider.Object, SettingConfig);

            Assert.Equal(@"<configuration>
  <appSettings>
    <add key=""key"" value=""val"" />
  </appSettings>
  <connectionStrings>
    <add name=""foo"" connectionString=""production"" providerName=""provider"" />
    <add name=""bar"" connectionString=""dev-bar"" providerName=""provider"" />
  </connectionStrings>
</configuration>", document.ToString().Trim());
        }

        [Fact]
        public void TransformWithAllConnectionStringSettingReplacesAllConnectionStrings() {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetConnectionStrings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "All",
                       Value = "production"
                   }
            });

            var document = DeploymentManager.Transform(mockSettingProvider.Object, SettingConfig);

            Assert.Equal(@"<configuration>
  <appSettings>
    <add key=""key"" value=""val"" />
  </appSettings>
  <connectionStrings>
    <add name=""foo"" connectionString=""production"" providerName=""provider"" />
    <add name=""bar"" connectionString=""production"" providerName=""provider"" />
  </connectionStrings>
</configuration>", document.ToString().Trim());
        }
    }
}
