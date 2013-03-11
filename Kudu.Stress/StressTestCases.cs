using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO  ;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts;
using Kudu.TestHarness;
using Kudu.Core.Deployment;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace Kudu.Stress
{
    class TestArtifacts
    {
        public ApplicationManager appManager = null;
        public W3wpResourceMonitor resourceMonitor = null;
        public TestRepository testRespository = null;
    }

    public class GitApplication
    {
        public string AppName;
        public string FirstCommitID;
        public string SecondCommitID;
        public string FileToModify;
        public string GitUrl;
        public string VerificationContent;
    }

    [TestClass]
    public class StressTestCases
    {
        static ConcurrentDictionary<string, TestArtifacts> testArtifactStore = new ConcurrentDictionary<string, TestArtifacts>();
        static object inititalizationLockObj = new object();

        // default the app beign tested to AspNetWebApplication"
        public GitApplication TestApplication = new GitApplication()
        {
            AppName = "AspNetWebApplication",
            FirstCommitID = "34d56bf3250b03c857ef8d89448e315215d3d479",
            SecondCommitID = "0e05d7968128fd4f289686ee24bac5d2529ae655",
            FileToModify = "AspNetWebApplication\\About.aspx",
            GitUrl = "https://github.com/kudustress/AspNetWebApplication.git",
            VerificationContent = "Welcome to ASP.NET!",
        };

        [TestMethod]
        public void StressGitPushScenario()
        {

            TestArtifacts testArtifacts = GetTestArtifacts(TestApplication.AppName, TestApplication.GitUrl);

            // modify a file in the repository and push
            ModifyFile(Path.Combine(testArtifacts.testRespository.PhysicalPath, TestApplication.FileToModify));
            Git.Commit(testArtifacts.testRespository.PhysicalPath, "stress test change");
            DoGitPush(testArtifacts.appManager, testArtifacts.testRespository, TestApplication.AppName, TestApplication.VerificationContent);
            
            testArtifacts.resourceMonitor.CheckAndLogResourceUsage();
        }

        [TestMethod]
        public void StressGitRedeployScenario()
        {
            TestArtifacts testArtifacts = GetTestArtifacts(TestApplication.AppName, TestApplication.GitUrl);

            StressGitPushScenario();
            DoGitRedeploy(testArtifacts.appManager, TestApplication.FirstCommitID, TestApplication.VerificationContent);
            DoGitRedeploy(testArtifacts.appManager, TestApplication.SecondCommitID, TestApplication.VerificationContent);
        }

        private void DoGitRedeploy(ApplicationManager appManager, string commitId, string verificationContent)
        {
            DeployResult result = null;
            try
            {
                appManager.DeploymentManager.DeployAsync(commitId).Wait();
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                result = results.Where(r => r.Id == commitId).FirstOrDefault();
            }
            catch(Exception ex)
            {
                string msg = string.Format("Redeploy operation failed for commit ID:  {0}.  Exception:  {1}", commitId, ex.ToString() );
                throw new ApplicationException(msg) ;
            }

            if (result == null)
            {
                string msg = string.Format("Redeploy operation completed but expected commit Id was not deployed.  Commit ID:  {0}.", commitId);
                throw new ApplicationException(msg);
            }

            StressUtils.VerifySite(appManager.SiteUrl, verificationContent);
        }

        private void DoGitPush(ApplicationManager appManager, TestRepository testRepository, string appName, string verificationContent)
        {
            appManager.GitDeploy(testRepository.PhysicalPath, "master");

            var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
            if (results[0].Status != Kudu.Core.Deployment.DeployStatus.Success)
            {
                string msg = string.Format("Deployment of app {0} failed.  Deployment count:  {1} .   Deployment Status:  {2}", appName, results.Count, results[0].Status);
                throw new ApplicationException(msg);
            }
            StressUtils.VerifySite(appManager.SiteUrl, verificationContent);
        }

        private void ModifyFile(string path)
        {
            using (StreamWriter writer = File.AppendText(path))
            {
                writer.Write(" "); 
            }
        }


        // create site and counters
        private TestArtifacts GetTestArtifacts(string testName, string gitUrl)
        {
            if (!testArtifactStore.ContainsKey(testName))
            {
                lock (inititalizationLockObj)
                {
                    if (!testArtifactStore.ContainsKey(testName))
                    {
                        // create site & start w3wp.exe with a request
                        ApplicationManager stressAppManager = ApplicationManager.CreateApplication(testName);
                        Uri siteUri;
                        Uri.TryCreate(new Uri(stressAppManager.SiteUrl), "hostingstart.html", out siteUri);
                        StressUtils.VerifySite(siteUri.AbsoluteUri, "successfully");

                        // create counter objects & do init log
                        W3wpResourceMonitor counterManager = new W3wpResourceMonitor(testName);
                        counterManager.CheckAndLogResourceUsage();

                        TestRepository testRepository = Git.Clone(testName, gitUrl);

                        testArtifactStore.TryAdd(testName, new TestArtifacts() { appManager = stressAppManager, resourceMonitor = counterManager, testRespository = testRepository});
                    }
                }
            }
            TestArtifacts testArtifacts;
            testArtifactStore.TryGetValue(testName, out testArtifacts);
            return testArtifacts;
        }

    }
}
