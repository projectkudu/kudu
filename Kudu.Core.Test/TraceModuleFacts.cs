using Kudu.Services.Web.Tracing;
using Xunit;

namespace Kudu.Core.Test
{
    public class TraceModuleFacts
    {
        [Theory]
        [InlineData("/api/siteextensions", true)]
        [InlineData("/api/siteextensions?a=b", true)]
        [InlineData("/api/siteextensions/foo", true)]
        [InlineData("/api/siteextensions/foo?a=b", true)]
        [InlineData("/api/siteextensionss", true)]
        [InlineData("/api/siteextensionss/foo", true)]
        [InlineData("/siteextensions", false)]
        [InlineData("/siteextensionss", false)]
        [InlineData("/api/deployments", false)]
        public void IsRbacWhiteListPathsTests(string path, bool expected)
        {
            // Test
            var actual = TraceModule.IsRbacWhiteListPaths(path);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
