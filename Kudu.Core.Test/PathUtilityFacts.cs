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
    }
}
