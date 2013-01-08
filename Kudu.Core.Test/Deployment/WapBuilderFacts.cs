using System.Collections.Generic;
using Kudu.Contracts.Settings;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Deployment.Test
{
    public class WapBuilderFacts
    {
        [Fact]
        public void BuildProjectAppendsBuildPropertiesToExec()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>();
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string> {{ "ExtensionsPath", @"C:\Program Files" }, {"flp", "Detailed" }});
            var wapBuilder = new WapBuilder(settings.Object, buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\solution-dir\sol-path");

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release /p:ExtensionsPath=""C:\Program Files"";flp=""Detailed"" /p:SolutionDir=""x:\solution-dir\\""", commandLineParams);
        }

        [Fact]
        public void BuildProjectAppendsBuildPropertiesAndExtraArgumentsToExec()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>();
            settings.Setup(s => s.GetValue(SettingsKeys.BuildArgs)).Returns("/extra_arg1 /extra_arg2");
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string> { { "ExtensionsPath", @"C:\Program Files" }, { "flp", "Detailed" } });
            var wapBuilder = new WapBuilder(settings.Object, buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\solution-dir\sol-path");

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release /p:ExtensionsPath=""C:\Program Files"";flp=""Detailed"" /p:SolutionDir=""x:\solution-dir\\"" /extra_arg1 /extra_arg2", commandLineParams);
        }

        [Fact]
        public void BuildProjectAppendsExtraArgumentsToExec()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>();
            settings.Setup(s => s.GetValue(SettingsKeys.BuildArgs)).Returns("/extra_arg1 /extra_arg2");
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string>(0));
            var wapBuilder = new WapBuilder(settings.Object, buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\solution-dir\sol-path");

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release /p:SolutionDir=""x:\solution-dir\\"" /extra_arg1 /extra_arg2", commandLineParams);
        }

        [Fact]
        public void BuildProjectDoesNotAppendPropertyProviderProperties()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>();
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string>(0));
            var wapBuilder = new WapBuilder(settings.Object, buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\solution-dir\sol-path");

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release /p:SolutionDir=""x:\solution-dir\\""", commandLineParams);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void BuildProjectDoesNotAppendSolutionDirIfSolutionPathIsNullOrEmpty(string solutionPath)
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>();
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string>(0));
            var wapBuilder = new WapBuilder(settings.Object, buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", solutionPath);

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release", commandLineParams);
        }
    }
}
