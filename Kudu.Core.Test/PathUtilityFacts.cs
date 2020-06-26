using Kudu.Contracts.Infrastructure;
using Kudu.Core.Infrastructure;
using Xunit;

namespace Kudu.Core.Test
{
    public class PathUtilityFacts
    {
        [Fact]
        public void CleanFixesOtherSlashes()
        {
            Assert.Equal(@"c:\foo\bar", PathUtilityFactory.Instance.CleanPath(@"c:/foo/bar"));
        }

        [Fact]
        public void CleanTrimsTrailingSlashes()
        {
            Assert.Equal(@"c:\foo\BAR", PathUtilityFactory.Instance.CleanPath(@"c:/foo/BAR/"));
            Assert.Equal(@"c:\foo\bar", PathUtilityFactory.Instance.CleanPath(@"c:\foo\bar\"));
        }

        [Fact]
        public void CleanTrimsWhiteSpaces()
        {
            Assert.Equal(@"c:\foo\BAR", PathUtilityFactory.Instance.CleanPath(@" c:/foo/BAR/ "));
            Assert.Equal(@"c:\foo\bar", PathUtilityFactory.Instance.CleanPath("\tc:\\foo\\bar\\"));
        }

        [Theory]
        [InlineData("https://tempuri.org/hello/world", "https://tempuri.org/hello/world")]
        [InlineData("https://tempuri.org/hello?world", "https://tempuri.org/hello?...")]
        [InlineData("/tempuri.org/hello/world", "/tempuri.org/hello/world")]
        [InlineData("/tempuri.org/hello?world", "/tempuri.org/hello?...")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void ObfuscatePathTests(string uri, string expected)
        {
            var actual = StringUtils.ObfuscatePath(uri);
            Assert.Equal(expected, actual);
        }
    }
}
