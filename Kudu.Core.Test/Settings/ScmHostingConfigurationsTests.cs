using System.Collections.Generic;
using Kudu.Core.Infrastructure;
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

        [Fact]
        public void ScmHostingConfigurationsEnabledByDefault()
        {
            // enabled / disabled by default
            Assert.True(VsHelper.UseMSBuild16(), "SCM_USE_MSBUILD_16");
            Assert.True(ScmHostingConfigurations.GetLatestDeploymentOptimized, "GetLatestDeploymentOptimized");
            Assert.True(ScmHostingConfigurations.DeploymentStatusCompleteFileEnabled, "DeploymentStatusCompleteFileEnabled");
            Assert.False(VsHelper.UseMSBuild1607(), "SCM_USE_MSBUILD_1607");

            ScmHostingConfigurations.Config = new Dictionary<string, string>
            {
                { "SCM_USE_MSBUILD_16", "0" },
                { "GetLatestDeploymentOptimized", "0" },
                { "DeploymentStatusCompleteFileEnabled", "0" },
                { "SCM_USE_MSBUILD_1607", "1" }
            };
            try
            {
                Assert.False(VsHelper.UseMSBuild16(), "SCM_USE_MSBUILD_16");
                Assert.False(ScmHostingConfigurations.GetLatestDeploymentOptimized, "GetLatestDeploymentOptimized");
                Assert.False(ScmHostingConfigurations.DeploymentStatusCompleteFileEnabled, "DeploymentStatusCompleteFileEnabled");
                Assert.True(VsHelper.UseMSBuild1607(), "SCM_USE_MSBUILD_1607");
            }
            finally
            {
                ScmHostingConfigurations.Config = null;
            }
        }
    }
}
