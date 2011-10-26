using System.IO;
using System.IO.Abstractions;
using System.Text;
using Kudu.Core.Deployment;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class AspNetConfigTransformerTest
    {
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
        public void TransformWithNoSettingsDoesNothing()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);
            var document = transformer.Transform(SettingConfig);

            Assert.Equal(SettingConfig.Trim(), document.ToString().Trim());
        }

        [Fact]
        public void TransformWithConfigurationFileWithNoAppSettingsAndConnectionStringsNodesShouldCreateAppSettingsButNotConnectionStrings()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetAppSettings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "key",
                       Value = "deployment"
                   }
            });

            mockSettingProvider.Setup(m => m.GetConnectionStrings()).Returns(new[] { 
                   new ConnectionStringSetting {
                       Name = "connection",
                       ConnectionString = "deployment-cs"
                   }
            });
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);
            var document = transformer.Transform(@"<configuration>
    <system.web>
        <compilation debug=""false"" targetFramework=""4.0"" />
    </system.web>
</configuration>
");

            Assert.Equal(@"<configuration>
  <system.web>
    <compilation debug=""false"" targetFramework=""4.0"" />
  </system.web>
  <appSettings>
    <add key=""key"" value=""deployment"" />
  </appSettings>
</configuration>", document.ToString().Trim());
        }

        [Fact]
        public void TransformWithExistingAppSettingReplacesSetting()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetAppSettings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "key",
                       Value = "deployment"
                   }
            });
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);
            var document = transformer.Transform(SettingConfig);

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
        public void TransformWithNewAppSettingAddsSetting()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetAppSettings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "kudu",
                       Value = "foo"
                   }
            });
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);
            var document = transformer.Transform(SettingConfig);

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
        public void TransformWithConnectionStringSettingReplacesConnectionString()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetConnectionStrings()).Returns(new[] { 
                   new ConnectionStringSetting {
                       Name = "foo",
                       ConnectionString = "production"
                   }
            });
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);
            var document = transformer.Transform(SettingConfig);

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
        public void PerformTransformationsDoesNothingIfWebConfigDoesNotExist()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);
            var mockFileInfo = new Mock<FileInfoBase>();
            mockFileInfo.Setup(m => m.Exists).Returns(false);
            var mockFileInfoFactory = new Mock<IFileInfoFactory>();
            mockFileInfoFactory.Setup(m => m.FromFileName(@"c:\foo\web.config")).Returns(mockFileInfo.Object);
            mockFileSystem.Setup(m => m.FileInfo).Returns(mockFileInfoFactory.Object);

            transformer.PerformTransformations(@"c:\foo");

            mockFileInfo.Verify(m => m.OpenRead(), Times.Never());
            mockFileInfo.Verify(m => m.Create(), Times.Never());
        }

        [Fact]
        public void PerformTransformationsTransformsConfigFileIfExists()
        {
            var mockSettingProvider = new Mock<IDeploymentSettingsManager>();
            mockSettingProvider.Setup(m => m.GetAppSettings()).Returns(new[] { 
                   new DeploymentSetting{
                       Key = "key",
                       Value = "deployment"
                   }
            });

            mockSettingProvider.Setup(m => m.GetConnectionStrings()).Returns(new[] { 
                   new ConnectionStringSetting {
                       Name = "foo",
                       ConnectionString = "deployment-cs"
                   }
            });
            var mockFileSystem = new Mock<IFileSystem>();
            var transformer = new AspNetConfigTransformer(mockFileSystem.Object, mockSettingProvider.Object);

            var ms = new Mock<MemoryStream> { CallBase = true };
            ms.Setup(m => m.Close()).Callback(() => { });

            var mockFileInfo = new Mock<FileInfoBase>();
            mockFileInfo.Setup(m => m.Exists).Returns(true);
            mockFileInfo.Setup(m => m.OpenRead()).Returns(GetStream(SettingConfig));
            mockFileInfo.Setup(m => m.Create()).Returns(ms.Object);

            var mockFileInfoFactory = new Mock<IFileInfoFactory>();
            mockFileInfoFactory.Setup(m => m.FromFileName(@"c:\foo\web.config")).Returns(mockFileInfo.Object);
            mockFileSystem.Setup(m => m.FileInfo).Returns(mockFileInfoFactory.Object);

            transformer.PerformTransformations(@"c:\foo");

            ms.Object.Seek(0, SeekOrigin.Begin);
            var content = new StreamReader(ms.Object).ReadToEnd();
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""key"" value=""deployment"" />
  </appSettings>
  <connectionStrings>
    <add name=""foo"" connectionString=""deployment-cs"" providerName=""provider"" />
    <add name=""bar"" connectionString=""dev-bar"" providerName=""provider"" />
  </connectionStrings>
</configuration>", content);
        }

        private Stream GetStream(string content)
        {
            return new MemoryStream(Encoding.Default.GetBytes(content));
        }
    }
}
