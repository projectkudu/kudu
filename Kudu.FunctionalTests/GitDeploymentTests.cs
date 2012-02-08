using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Web.Infrastructure;
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
                string randomTestName = GetRandomWebSiteName(repoName);
                using (Git.Clone(randomTestName, repoCloneUrl))
                {
                    ApplicationManager.Run(randomTestName, appManager =>
                    {
                        // Act
                        appManager.GitDeploy(randomTestName, defaultBranchName);
                        var results = appManager.DeploymentManager.GetResults().ToList();

                        // Assert
                        Assert.Equal(1, results.Count);
                        Assert.Equal(DeployStatus.Success, results[0].Status);
                        Verify(appManager.SiteUrl, verificationText, expectedResponseCode);
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

        public void Verify(string url, string content = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var client = new HttpClient();
            var response = client.GetAsync(url).Result;
            Assert.Equal(statusCode, response.StatusCode);

            if (content != null)
            {
                var responseBody = response.Content.ReadAsStringAsync().Result;
                Assert.True(responseBody.Contains(content));
            }
        }

        private string GetRandomWebSiteName(string webSiteName)
        {
            string guid = System.Guid.NewGuid().ToString("N");
            if (webSiteName != null)
            {
                return string.Concat(webSiteName, guid);
            }

            return guid;
        }
    }
}
