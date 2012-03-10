using System.Linq;
using System.Net;
using System.Net.Http;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class DeploymentManagerTests
    {
        [Fact]
        public void DeploymentApisReturn404IfDeploymentIdDoesntExist()
        {
            string appName = "DeploymentApisReturn404IfDeploymentIdDoesntExist";

            ApplicationManager.Run(appName, appManager =>
            {
                string id = "foo";
                var ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeleteAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeployAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeployAsync(id, clean: true).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.GetLogEntriesAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.GetResultAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, "fakeId").Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);
            });
        }

        [Fact]
        public void DeploymentApis()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "DeploymentApis";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(1, results.Count);
                    var result = results[0];
                    Assert.Equal("Raquel Almeida", result.Author);
                    Assert.Equal("raquel_soares@msn.com", result.AuthorEmail);
                    Assert.True(result.Current);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.NotNull(result.Url);
                    Assert.NotNull(result.LogUrl);

                    ICredentials cred = appManager.DeploymentManager.Credentials;
                    KuduAssert.VerifyUrl(result.Url, cred);
                    KuduAssert.VerifyUrl(result.LogUrl, cred);

                    var resultAgain = appManager.DeploymentManager.GetResultAsync(result.Id).Result;
                    Assert.Equal("Raquel Almeida", resultAgain.Author);
                    Assert.Equal("raquel_soares@msn.com", resultAgain.AuthorEmail);
                    Assert.True(resultAgain.Current);
                    Assert.Equal(DeployStatus.Success, resultAgain.Status);
                    Assert.NotNull(resultAgain.Url);
                    Assert.NotNull(resultAgain.LogUrl);
                    KuduAssert.VerifyUrl(resultAgain.Url, cred);
                    KuduAssert.VerifyUrl(resultAgain.LogUrl, cred);

                    // Redeploy
                    appManager.DeploymentManager.DeployAsync(result.Id).Wait();

                    // Clean deploy
                    appManager.DeploymentManager.DeployAsync(result.Id, clean: true).Wait();

                    var entries = appManager.DeploymentManager.GetLogEntriesAsync(result.Id).Result.ToList();

                    Assert.Equal(3, entries.Count);

                    var nested = appManager.DeploymentManager.GetLogEntryDetailsAsync(result.Id, entries[1].Id).Result.ToList();

                    Assert.Equal(1, nested.Count);

                    // Can't delete the active one
                    var ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeleteAsync(result.Id).Wait());
                    Assert.Equal("Response status code does not indicate success: 409 (Conflict).", ex.Message);

                    repo.WriteFile("HelloWorld.txt", "This is a test");
                    Git.Commit(repo.PhysicalPath, "Another commit");
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    string newId = results[1].Id;

                    // Delete one
                    appManager.DeploymentManager.DeleteAsync(newId).Wait();

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(1, results.Count);
                    Assert.NotEqual(newId, results[0].Id);
                });
            }
        }
    }
}
