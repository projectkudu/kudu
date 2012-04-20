using System;
using System.Text;
using Kudu.Services.Infrastructure;
using Xunit;

namespace Kudu.Core.Test
{
    public class AuthUtilityFacts
    {
        public class TryExtractBasicAuthUserFromHeader
        {
            [Fact]
            public void FailsToParseIfHeaderIsNull()
            {
                // Arrange
                string username;

                // Act
                var result = AuthUtility.TryExtractBasicAuthUserFromHeader(null, out username);

                // Assert
                Assert.False(result);
                Assert.Null(username);
            }

            [Fact]
            public void FailsToParseIfHeaderIsSchemeNotBasic()
            {
                // Arrange
                string username;

                // Act
                var result = AuthUtility.TryExtractBasicAuthUserFromHeader("Digest: something", out username);

                // Assert
                Assert.False(result);
                Assert.Null(username);
            }

            [Fact]
            public void ParsesBasicAuthHeader()
            {
                // Arrange
                var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
                string username;

                // Act
                var result = AuthUtility.TryExtractBasicAuthUserFromHeader("Basic " + payload, out username);

                // Assert
                Assert.True(result);
                Assert.Equal("user", username);
            }
        }

        public class TryParseBasicAuthUserFromHeaderParameter
        {
            [Fact]
            public void ParsesUsername()
            {
                // Arrange
                var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
                string username;

                // Act
                var result = AuthUtility.TryParseBasicAuthUserFromHeaderParameter(payload, out username);

                // Assert
                Assert.True(result);
                Assert.Equal("user", username);
            }

            [Fact]
            public void FailsToParseIfNoSeparator()
            {
                // Arrange
                var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("userpassword"));
                string username;

                // Act
                var result = AuthUtility.TryParseBasicAuthUserFromHeaderParameter(payload, out username);

                // Assert
                Assert.False(result);
                Assert.Null(username);
            }

            [Fact]
            public void FailsToParseIfNoUsername()
            {
                // Arrange
                var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(":password"));
                string username;

                // Act
                var result = AuthUtility.TryParseBasicAuthUserFromHeaderParameter(payload, out username);

                // Assert
                Assert.False(result);
                Assert.Null(username);
            }
        }
    }
}
