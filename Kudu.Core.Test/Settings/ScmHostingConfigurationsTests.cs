using Kudu.Core.Settings;
using Xunit;

namespace Kudu.Core.Test.Settings
{
    public class ScmHostingConfigurationsTests
    {
        [Theory]
        [InlineData("a=b,foo=bar,c=d", 3, "foo", "bar")]
        [InlineData("a=b,foo=bar,c=d", 3, "FoO", "bar")]
        [InlineData("foo=bar,c=d", 2, "c", "d")]
        [InlineData("a=,foo=bar,c=d", 2, "a", null)]
        public void ScmHostingConfigurationsParseTests(string settings, int count, string key, string value)
        {
            var dict = ScmHostingConfigurations.Parse(settings);
            Assert.Equal(count, dict.Count);
            if (!string.IsNullOrEmpty(key))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Assert.Equal(value, dict[key]);
                }
                else
                {
                    Assert.False(dict.ContainsKey(key));
                }
            }
        }
    }
}
