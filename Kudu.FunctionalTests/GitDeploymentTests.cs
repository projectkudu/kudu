using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    public class GitDeploymentTests
    {       
        static GitTestConfig gitTestConfig = new GitTestConfig(PathHelper.GitDeploymentTestsFile);
    
        [Theory]
        [PropertyData("GetTestData")]
        public void PushAndDeployApps(string name, string repoName,
                                      string repoUrl, string repoCloneUrl,
                                      string defaultBranchName, string verificationText, 
                                      HttpStatusCode expectedResponseCode, bool skip)
        {
            if (!skip)
            {
                string randomTestName = KuduUtils.GetRandomWebsiteName(repoName);
                using (var repo = Git.Clone(randomTestName, repoCloneUrl))
                {
                    ApplicationManager.Run(randomTestName, appManager =>
                    {
                        // Act
                        appManager.AssertGitDeploy(repo.PhysicalPath, defaultBranchName);
                        var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                        // Assert
                        Assert.Equal(1, results.Count);
                        Assert.Equal(DeployStatus.Success, results[0].Status);
                        KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText, expectedResponseCode);
                    });
                }
                Debug.Write(string.Format("Test completed: {0}\n", name));
            }
            else
            {
                Debug.Write(string.Format("Test skipped: {0}\n", name));
            }
        }

        public static IEnumerable<object[]> GetTestData
        {
            get
            {
                return gitTestConfig.GetTests();
            }
        }
    }
}
