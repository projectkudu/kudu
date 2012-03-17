using Kudu.Core.Infrastructure;
using Xunit;

namespace Kudu.Core.Test
{
    public class PathUtilityFacts
    {
        [Fact]
        public void NormalizeFixesOtherSlashes()
        {
            Assert.Equal(@"C:\FOO\BAR", PathUtility.NormalizePath(@"c:/foo/bar"));
        }

        [Fact]
        public void NormalizeTrimsTrailingSlashes()
        {
            Assert.Equal(@"C:\FOO\BAR", PathUtility.NormalizePath(@"c:/foo/bar/"));
            Assert.Equal(@"C:\FOO\BAR", PathUtility.NormalizePath(@"c:\foo\bar\"));
        }
    }
}
