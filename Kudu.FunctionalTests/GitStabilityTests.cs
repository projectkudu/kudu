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
            string cloneUrl = "https://github.com/KuduApps/HelloKudu.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, commitId: "2370e44"))
            {
                for (int i = 0; i < 5; i++)
                {
                    string applicationName = KuduUtils.GetRandomWebsiteName(repositoryName + i);
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
    }
}
