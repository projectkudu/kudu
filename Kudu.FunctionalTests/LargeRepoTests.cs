using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Dropbox;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit.Extensions;
using TimeoutException = System.TimeoutException;

namespace Kudu.FunctionalTests
{
    class LargeRepoTests
    {

        [Theory]
        //[InlineData("https://github.com/KuduApps/moodle", "master","http://docs.moodle.org/en/Installing_Moodle", "3a8c4380", "README.txt")]
        [InlineData("https://github.com/KuduApps/LargeStaticSite", "master", "Tsagaan Agui", "5fab40d", "Mongolia.html")]
        //[InlineData("https://github.com/KuduApps/Kentico", "master", "Database Setup", "0dcdbef", "cmsinstall/install.aspx")]
        //[InlineData("https://kudutest@bitbucket.org/kudutest/moodlegit.git", "master", "http://docs.moodle.org/en/Installing_Moodle", "3a8c4380", "README.txt")]
        [InlineData("https://kudutest@bitbucket.org/kudutest/largestaticsitegit.git", "master", "Tsagaan Agui", "5fab40d", "Mongolia.html")]
        //[InlineData("https://kudutest@bitbucket.org/kudutest/kenticogit.git", "master", "Database Setup", "0dcdbef", "cmsinstall/install.aspx")]
        public void DeployLargeRepo(string repoCloneUrl, string defaultBranchName, string verificationText, string commitId, string resourcePath)
        {

            GitDeploymentTests.PushAndDeployApps(repoCloneUrl, defaultBranchName, verificationText, HttpStatusCode.OK, commitId, resourcePath, "", "", true);

        }

        [Theory]
        //[InlineData("/Moodle", "Moodle","http://docs.moodle.org/en/Installing_Moodle", "README.txt")]
        //[InlineData("/Kentico", "Kentico", "Database Setup", "cmsinstall/install.aspx")]
        [InlineData("/LargeStaticSite", "LargeStaticSite", "Tsagaan Agui", "Mongolia.html")]
        public void DeployLargeRepoFromDropbox(string repoPath, string appName, string verificationText, string resourcePath)
        {
            using (new LatencyLogger("PushAndDeployApps - " + appName))
            {
                var db = new DropboxTests();
                {
                    DropboxTests.OAuthInfo oauth = db.GetOAuthInfo();

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

                        KuduAssert.VerifyUrlAsync(appManager.SiteUrl + resourcePath, verificationText).Wait();
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
