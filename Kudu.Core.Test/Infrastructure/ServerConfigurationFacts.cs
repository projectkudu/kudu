using Kudu.Services.Web;
using System;
using Xunit;


namespace Kudu.Core.Infrastructure.Test
{
    public class ServerConfigurationFacts
    {

        const string AppPoolIdKey = "APP_POOL_ID";
        const string SiteNameKey = "WEBSITE_SITE_NAME";
        [Fact]
        public void ServerConfigurationNoneSet()
        {
            WithEnvVarUnset(SiteNameKey, () =>
            {
                WithEnvVarUnset(AppPoolIdKey, () =>
                    {
                        var config = new ServerConfiguration();
                        Assert.Equal(String.Empty, config.ApplicationName);
                        Assert.Equal("git", config.GitServerRoot);
                    });
            });
        }

        [Fact]
        public void ServerConfigurationWebSiteNameSet()
        {
            // WEBSITE_NAME overrides app pool_id
            var rightValue = "right-" + Guid.NewGuid().ToString();
            var wrongValue = "wrong-" + Guid.NewGuid().ToString();
            WithEnvVarSet(SiteNameKey, rightValue, () =>
            {
                WithEnvVarSet(AppPoolIdKey, wrongValue, () =>
                {
                    var config = new ServerConfiguration();
                    Assert.Equal(rightValue, config.ApplicationName);
                    Assert.Equal(rightValue + ".git", config.GitServerRoot);
                });
            });
        }

        [Fact]
        public void ServerConfigurationAppPoolIdNameSet()
        {
            var rightValue = "right-" + Guid.NewGuid().ToString();
            WithEnvVarUnset(SiteNameKey, () =>
            {
                WithEnvVarSet(AppPoolIdKey, rightValue, () =>
                {
                    var config = new ServerConfiguration();
                    Assert.Equal(rightValue, config.ApplicationName);
                    Assert.Equal(rightValue + ".git", config.GitServerRoot);
                });
            });
        }

        private void WithEnvVarUnset(string name, Action act)
        {
            WithEnvVarSet(name, null, act);
        }

        private void WithEnvVarSet(string name,string value, Action act)
        {
            string oldValue = System.Environment.GetEnvironmentVariable(name);
            System.Environment.SetEnvironmentVariable(name, value);
            try
            {
                act();
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(name, oldValue);
            }

        }
    }
}
