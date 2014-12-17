using System;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Moq;
using Xunit;
using Xunit.Extensions;

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

        [Theory]
        [InlineData("e é à a")]
        public void CheckExtendedAsciiCharactersInWebJobLogging(string data)
        {
            // Arrange
            var trace = Mock.Of<ITracer>();
            var executable = new Executable(@"cmd.exe", string.Empty, TimeSpan.MaxValue);

            // Act
            executable.ExecuteReturnExitCode(trace, s =>
            {

                // Assert
                Assert.Equal(data, s);

            }, null, "/c \"echo " + data + "\"");
        }
    }
}
