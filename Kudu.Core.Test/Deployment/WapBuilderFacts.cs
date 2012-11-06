using System.Collections.Generic;
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
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string> {{ "ExtensionsPath", @"C:\Program Files" }, {"flp", "Detailed" }});
            var wapBuilder = new WapBuilder(buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\nuget-cache-path", @"x:\solution-dir\sol-path");

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release /p:ExtensionsPath=""C:\Program Files"";flp=""Detailed"" /p:SolutionDir=""x:\solution-dir\\""", commandLineParams);
        }

        [Fact]
        public void BuildProjectDoesNotAppendPropertyProviderProperties()
        {
            // Arrange
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string>(0));
            var wapBuilder = new WapBuilder(buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\nuget-cache-path", @"x:\solution-dir\sol-path");

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release /p:SolutionDir=""x:\solution-dir\\""", commandLineParams);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void BuildProjectDoesNotAppendSolutionDirIfSolutionPathIsNullOrEmpty(string solutionPath)
        {
            // Arrange
            var buildPropertyProvider = new Mock<IBuildPropertyProvider>();
            buildPropertyProvider.Setup(s => s.GetProperties()).Returns(new Dictionary<string, string>(0));
            var wapBuilder = new WapBuilder(buildPropertyProvider.Object, @"x:\source-path", @"x:\project-path", @"x:\temp-path", @"x:\nuget-cache-path", solutionPath);

            // Act
            var commandLineParams = wapBuilder.GetMSBuildArguments(@"x:\temp-path\some-guid");

            // Assert
            Assert.Equal(@"""x:\project-path"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""x:\temp-path\some-guid"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release", commandLineParams);
        }
    }
}
