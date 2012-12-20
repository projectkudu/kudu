using System;
using System.Collections.Generic;
using System.IO;
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
        public static IEnumerable<object[]> AspNetApps
        {
            get
            {
                // RepoCloneUrl, DefaultBranchName, VerificationText, ExpectedResponseCode, VerificationLogText
                yield return new object[] { "https://github.com/KuduApps/Orchard.git", "master", 
                    "Welcome to Orchard", HttpStatusCode.OK, "" };

                yield return new object[] { "https://github.com/KuduApps/ProjectWithNoSolution.git", "master", 
                    "Project without solution", HttpStatusCode.OK, "" };

                yield return new object[] { "https://github.com/KuduApps/HiddenFoldersAndFiles.git", "master", 
                    "Hello World", HttpStatusCode.OK, "" };

                yield return new object[] { "https://github.com/KuduApps/WebSiteInSolution.git", "master", 
                    "SomeDummyLibrary.Class1", HttpStatusCode.OK, "" };

                yield return new object[] { "https://github.com/KuduApps/kuduglob.git", "master", 
                    "ASP.NET MVC", HttpStatusCode.OK, "酷度" };

                yield return new object[] { "https://github.com/KuduApps/AppWithPostBuildEvent.git", "master", 
                    "Hello Kudu", HttpStatusCode.OK, "Deployment successful" };
            }
        }

        [Theory]
        [PropertyData("AspNetApps")]
        public void PushAndDeployAspNetApps(string repoCloneUrl, string defaultBranchName, string verificationText,
                                      HttpStatusCode expectedResponseCode, string verificationLogText = null)
        {
            PushAndDeployApps(repoCloneUrl, defaultBranchName, verificationText, expectedResponseCode, verificationLogText);
        }

        public static IEnumerable<object[]> NodeApps
        {
            get
            {
                // RepoCloneUrl, DefaultBranchName, VerificationText, ExpectedResponseCode, VerificationLogText
                yield return new object[] { "https://github.com/KuduApps/Express-Template.git", "master", 
                    "Modify this template to jump-start your Node.JS Express Web Pages application", HttpStatusCode.OK, "" };
            }
        }

        [Theory]
        [PropertyData("NodeApps")]
        public void PushAndDeployNodeApps(string repoCloneUrl, string defaultBranchName, string verificationText,
                                      HttpStatusCode expectedResponseCode, string verificationLogText = null)
        {
            // Ensure node is installed.
            Assert.Contains("nodejs", Environment.GetEnvironmentVariable("Path"), StringComparison.OrdinalIgnoreCase);
            PushAndDeployApps(repoCloneUrl, defaultBranchName, verificationText, expectedResponseCode, verificationLogText);
        }

        [Fact]
        public void CustomDeploymentScriptShouldHaveDeploymentSetting()
        {
            var verificationLogText = "Settings Were Set Properly";
            var repoCloneUrl = "https://github.com/KuduApps/CustomDeploymentSettingsTest.git";

            string randomTestName = KuduUtils.GetRandomWebsiteName(Path.GetFileNameWithoutExtension(repoCloneUrl));
            ApplicationManager.Run(randomTestName, appManager =>
            {
                appManager.SettingsManager.SetValue("TESTED_VAR", verificationLogText).Wait();

                // Act
                using (TestRepository testRepository = Git.Clone(randomTestName, repoCloneUrl))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, "master");
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyLogOutput(appManager, results[0].Id, verificationLogText.Trim());
            });
        }

        public static IEnumerable<object[]> PhpApps
        {
            get
            {
                // RepoCloneUrl, DefaultBranchName, VerificationText, ExpectedResponseCode, VerificationLogText
                yield return new object[] { "https://github.com/KuduApps/PhpBBweb.git", "master", 
                    "Welcome to phpBB3", HttpStatusCode.OK, "" };
            }
        }

        [Theory(Skip="Omitting PHP test since it's hard to set up and doesn't cover anything interesting to Kudu")]
        [PropertyData("PhpApps")]
        public void PushAndDeployPhpApps(string repoCloneUrl, string defaultBranchName, string verificationText,
                                         HttpStatusCode expectedResponseCode, string verificationLogText = null)
        {
            PushAndDeployApps(repoCloneUrl, defaultBranchName, verificationText, expectedResponseCode, verificationLogText);
        }

        private static void PushAndDeployApps(string repoCloneUrl, string defaultBranchName,
                                              string verificationText, HttpStatusCode expectedResponseCode, string verificationLogText)
        {
            string randomTestName = KuduUtils.GetRandomWebsiteName(Path.GetFileNameWithoutExtension(repoCloneUrl));
            ApplicationManager.Run(randomTestName, appManager =>
            {
                // Act
                using (TestRepository testRepository = Git.Clone(randomTestName, repoCloneUrl))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, defaultBranchName);
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText, expectedResponseCode);
                if (!String.IsNullOrEmpty(verificationLogText))
                {
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, verificationLogText.Trim());
                }
            });
        }
    }
}
