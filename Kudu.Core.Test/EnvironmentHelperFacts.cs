using Kudu.Core.Helpers;
using Xunit;

namespace Kudu.Core.Test
{
    public class EnvironmentHelperFacts
    {
        [Theory]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("1", true)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("True", true)]
        [InlineData("0", false)]
        [InlineData("RandomValue", false)]
        [InlineData("https://microsoft.com", true)]
        [InlineData("/random/path", false)]
        public void IsValidRunFromPackageTests(string runFromPackageValue, bool expected)
        {
            bool actual = EnvironmentHelper.IsValidRunFromPackage(runFromPackageValue);

            Assert.Equal(expected, actual);
        }
    }
}
