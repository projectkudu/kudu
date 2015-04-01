using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Services;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;
using TimeoutException = System.TimeoutException;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class LargeRepoTests
    {
        private static readonly bool shouldRunLargeRepoTests = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_LARGE_REPO_TESTS"));

        [Theory]
        [InlineData("https://github.com/kudutest1/moodle.git", "master", "http://docs.moodle.org/en/Installing_Moodle", "3a8c4380", "README.txt")]
        [InlineData("https://github.com/kudutest2/Kentico.git", "master", "Database Setup", "0dcdbef", "cmsinstall/install.aspx")]
        [InlineData("https://bitbucket.org/kudutest1/moodlegit.git", "master", "http://docs.moodle.org/en/Installing_Moodle", "3a8c4380", "README.txt")]
        [InlineData("https://bitbucket.org/kudutest2/kenticogit.git", "master", "Database Setup", "0dcdbef", "cmsinstall/install.aspx")]
        public void DeployLargeRepo(string repoCloneUrl, string defaultBranchName, string verificationText, string commitId, string resourcePath)
        {
            if (!shouldRunLargeRepoTests)
            {
                return;
            }
            GitDeploymentTests.PushAndDeployApps(repoCloneUrl, 
                                                 defaultBranchName, 
                                                 verificationText, 
                                                 HttpStatusCode.OK, 
                                                 verificationLogText: commitId, 
                                                 resourcePath: resourcePath, 
                                                 deleteSCM: true);
        }

        [Theory]
        [InlineData("/moodle", "Moodle","http://docs.moodle.org/en/Installing_Moodle", "README.txt")]
        [InlineData("/Kentico", "Kentico", "Database Setup", "cmsinstall/install.aspx")]
        public void DeployLargeRepoFromDropbox(string repoPath, string appName, string verificationText, string resourcePath)
        {
            if (!shouldRunLargeRepoTests)
            {
                return;
            }
            using (new LatencyLogger("PushAndDeployApps - " + appName))
            {
                var db = new DropboxTests();
                {
                    DropboxTests.OAuthInfo oauth = db.GetOAuthInfo(appName);

                    if (oauth == null)
                    {
                        // only run in private kudu
                        return;
                    }
                    DropboxDeployInfo deploy;
                    DropboxTests.AccountInfo account = db.GetAccountInfo(oauth);
                    using (new LatencyLogger("DropboxGetDeployInfo - " + appName))
                    {
                        deploy = db.GetDeployInfo(repoPath, oauth, account);
                    }

                    ApplicationManager.Run(appName, appManager =>
                    {
                        const int timeoutInMinutes = 100;
                        DateTime stopTime = DateTime.Now.AddMinutes(timeoutInMinutes);

                        var client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);

                        using (new LatencyLogger("DropboxDeploy - " + appName))
                        {
                            client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy);
                            List<DeployResult> results;
                            do
                            {
                                Thread.Sleep(5000);
                                if (DateTime.Now > stopTime)
                                {
                                    throw new TimeoutException("Terminating test. Current test took too long.");
                                }
                                results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                                TestTracer.Trace(results[0].Progress);

                            } while ((results[0]).Status != DeployStatus.Success && (results[0]).Status != DeployStatus.Failed);
                        }

                        KuduAssert.VerifyUrl(appManager.SiteUrl + resourcePath, verificationText);
                        using (new LatencyLogger("DropboxScmDelete - " + appName))
                        {
                            appManager.RepositoryManager.Delete(deleteWebRoot: false, ignoreErrors: false).Wait();
                        }
                    });
                }

            }
        }

    }
}
