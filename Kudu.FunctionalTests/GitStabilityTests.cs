using System.Linq;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
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
                string upTime = appManager.GetKuduUpTime();

                // 1/29/2013 12:36 AM (00:39:09.1443904)
                Assert.Contains("/", upTime);
                Assert.Contains(":", upTime);
                Assert.Contains(".", upTime);
            });
        }
    }
}
