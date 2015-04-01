using Kudu.Contracts.Settings;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class ExecutableExtensionsFacts
    {
        [Fact]
        public void AddToPathAddsPathEnvironmentValueIfItDoesNotExist()
        {
            // Arrange
            var executable = new Executable(@"x:\some.exe", @"x:\some-dir", DeploymentSettingsExtension.DefaultCommandIdleTimeout);

            // Act
            executable.PrependToPath(new[] { @"x:\path1", @"y:\path1\path2" });

            // Assert
            Assert.Equal(@"x:\path1;y:\path1\path2", executable.EnvironmentVariables["PATH"]);
        }

        [Theory]
        [InlineData(@"c:\foo;c:\windows\Microsoft.net\framework", new[] { @"x:\path1", @"y:\path1\path2" },
                    @"x:\path1;y:\path1\path2;c:\foo;c:\windows\Microsoft.net\framework")]
        [InlineData(@"c:\foo;c:\windows\Microsoft.net\framework;", new[] { @"x:\path1", @"y:\path1\path2" },
                    @"x:\path1;y:\path1\path2;c:\foo;c:\windows\Microsoft.net\framework;")]
        [InlineData(@"c:\foo;c:\windows\Microsoft.net\framework\;", new[] { @"x:\path1", @"y:\path1\path2\" },
                    @"x:\path1;y:\path1\path2\;c:\foo;c:\windows\Microsoft.net\framework\;")]
        public void AddToPathAppendsPathEnvironmentValue(string current, string[] input, string expected)
        {
            // Arrange
            var executable = new Executable(@"x:\some.exe", @"x:\some-dir", DeploymentSettingsExtension.DefaultCommandIdleTimeout);
            executable.EnvironmentVariables["PATH"] = current;

            // Act
            executable.PrependToPath(input);

            // Assert
            Assert.Equal(expected, executable.EnvironmentVariables["PATH"]);
        }
    }
}
