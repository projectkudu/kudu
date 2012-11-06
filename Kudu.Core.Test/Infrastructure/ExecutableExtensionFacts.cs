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
            var executable = new Executable(@"x:\some.exe", @"x:\some-dir");

            // Act
            executable.AddToPath(@"x:\path1", @"y:\path1\path2");

            // Assert
            Assert.Equal(@"x:\path1;y:\path1\path2", executable.EnvironmentVariables["PATH"]);
        }

        [Theory]
        [InlineData(@"c:\foo;c:\windows\Microsoft.net\framework", new[] { @"x:\path1", @"y:\path1\path2" },
                    @"c:\foo;c:\windows\Microsoft.net\framework;x:\path1;y:\path1\path2")]
        [InlineData(@"c:\foo;c:\windows\Microsoft.net\framework;", new[] { @"x:\path1", @"y:\path1\path2" },
                    @"c:\foo;c:\windows\Microsoft.net\framework;x:\path1;y:\path1\path2")]
        [InlineData(@"c:\foo;c:\windows\Microsoft.net\framework\;", new[] { @"x:\path1", @"y:\path1\path2\" },
                    @"c:\foo;c:\windows\Microsoft.net\framework\;x:\path1;y:\path1\path2\")]
        public void AddToPathAppendsPathEnvironmentValue(string current, string[] input, string expected)
        {
            // Arrange
            var executable = new Executable(@"x:\some.exe", @"x:\some-dir");
            executable.EnvironmentVariables["PATH"] = current;

            // Act
            executable.AddToPath(input);

            // Assert
            Assert.Equal(expected, executable.EnvironmentVariables["PATH"]);
        }
    }
}
