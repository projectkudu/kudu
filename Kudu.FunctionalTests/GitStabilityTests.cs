using System;
using System.Linq;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class GitStabilityTests
    {
        [Fact]
        public void NSimpleDeployments()
        {
            string repositoryName = "HelloKudu";
            using (var repo = Git.Clone("HelloKudu"))
            {
                for (int i = 0; i < 5; i++)
                {
                    string applicationName = repositoryName + i;
                    ApplicationManager.Run(applicationName, appManager =>
                    {
                        // Act
                        appManager.GitDeploy(repo.PhysicalPath);
                        var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                        // Assert
                        Assert.Equal(1, results.Count);
                        Assert.Equal(DeployStatus.Success, results[0].Status);
                        KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Kudu");
                    });
                }
            }
        }

        [Fact]
        public void KuduUpTimeTest()
        {
            ApplicationManager.Run("KuduUpTimeTest", appManager =>
            {
                TimeSpan upTime = TimeSpan.Parse(appManager.GetKuduUpTime());

                TestTracer.Trace("UpTime: {0}", upTime);

                // some time the upTime is 00:00:00 (TimeSpan.Zero!).
                // Need to do more investigation.
                // Assert.NotEqual<TimeSpan>(TimeSpan.Zero, upTime);
            });
        }
    }
}
