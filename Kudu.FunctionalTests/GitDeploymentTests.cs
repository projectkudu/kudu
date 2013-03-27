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
        // ASP.NET apps

        [Fact]
        public void PushAndDeployAspNetAppOrchard()
        {
            PushAndDeployApps("Orchard", "master", "Welcome to Orchard", HttpStatusCode.OK, "");
        }

        [Fact]
        public void PushAndDeployAspNetAppProjectWithNoSolution()
        {
            PushAndDeployApps("ProjectWithNoSolution", "master", "Project without solution", HttpStatusCode.OK, "");
        }

        [Fact]
        public void PushAndDeployAspNetAppHiddenFoldersAndFiles()
        {
            PushAndDeployApps("HiddenFoldersAndFiles", "master", "Hello World", HttpStatusCode.OK, "");
        }

        [Fact]
        public void PushAndDeployWebApiApp()
        {
            PushAndDeployApps("Dev11_Net45_Mvc4_WebAPI", "master", "HelloWorld", HttpStatusCode.OK, "", resourcePath: "api/values", httpMethod: "POST", jsonPayload: "\"HelloWorld\"");
        }

        [Fact]
        public void PushAndDeployAspNetAppWebSiteInSolution()
        {
            PushAndDeployApps("WebSiteInSolution", "master", "SomeDummyLibrary.Class1", HttpStatusCode.OK, "");
        }

        [Fact]
        public void PushAndDeployAspNetAppKuduGlob()
        {
            PushAndDeployApps("kuduglob", "master", "ASP.NET MVC", HttpStatusCode.OK, "酷度");
        }

        [Fact]
        public void PushAndDeployAspNetAppAppWithPostBuildEvent()
        {
            PushAndDeployApps("AppWithPostBuildEvent", "master", "Hello Kudu", HttpStatusCode.OK, "Deployment successful");
        }

        // Node apps

        [Fact]
        public void PushAndDeployNodeAppExpress()
        {
            // Ensure node is installed.
            Assert.Contains("nodejs", Environment.GetEnvironmentVariable("Path"), StringComparison.OrdinalIgnoreCase);

            PushAndDeployApps("Express-Template", "master", "Modify this template to jump-start your Node.JS Express Web Pages application", HttpStatusCode.OK, "");
        }

        //Entity Framework 4.5 MVC Project with SQL Compact DB (.sdf file) 
        //and Metadata Artifact Processing set to 'Embed in Assembly'
        [Fact]
        public void PushAndDeployEFMVC45AppSqlCompactMAPEIA()
        {
            PushAndDeployApps("MvcApplicationEFSqlCompact", "master", "Reggae", HttpStatusCode.OK, "");
        }

        // Other apps

        [Fact]
        public void CustomDeploymentScriptShouldHaveDeploymentSetting()
        {
            var verificationLogText = "Settings Were Set Properly";

            string randomTestName = "CustomDeploymentScriptShouldHaveDeploymentSetting";
            ApplicationManager.Run(randomTestName, appManager =>
            {
                appManager.SettingsManager.SetValue("TESTED_VAR", verificationLogText).Wait();

                // Use a custom location for the .deployment file
                appManager.SettingsManager.SetValue("SCM_DEPLOYMENT_FILE", @"sub\sub2\MyCustomIniFile.txt").Wait();

                // Act
                using (TestRepository testRepository = Git.Clone("CustomDeploymentSettingsTest"))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, "master");
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);

                // Also validate custom script output supports unicode
                KuduAssert.VerifyLogOutput(appManager, results[0].Id, new string[] { verificationLogText, "酷度酷度2" });
            });
        }

        [Fact]
        public void PushAndDeployMVCAppWithLatestNuget()
        {
            PushAndDeployApps("MVCAppWithLatestNuget", "master", "MVCAppWithLatestNuget", HttpStatusCode.OK, "Deployment successful");
        }

        //Common code
        private static void PushAndDeployApps(string repoCloneUrl, string defaultBranchName,
                                              string verificationText, HttpStatusCode expectedResponseCode, string verificationLogText,
                                              string resourcePath = "", string httpMethod = "GET", string jsonPayload = "")
        {
            using (new LatencyLogger("PushAndDeployApps - " + repoCloneUrl))
            {
                Uri uri;
                if (!Uri.TryCreate(repoCloneUrl, UriKind.Absolute, out uri))
                {
                    uri = null;
                }

                string randomTestName = uri != null ? Path.GetFileNameWithoutExtension(repoCloneUrl) : repoCloneUrl;
                ApplicationManager.Run(randomTestName, appManager =>
                {
                    // Act
                    using (TestRepository testRepository = Git.Clone(randomTestName, uri != null ? repoCloneUrl : null))
                    {
                        using (new LatencyLogger("GitDeploy"))
                        {
                            appManager.GitDeploy(testRepository.PhysicalPath, defaultBranchName);
                        }
                    }
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    var url = new Uri(new Uri(appManager.SiteUrl), resourcePath);
                    KuduAssert.VerifyUrl(url.ToString(), verificationText, expectedResponseCode, httpMethod, jsonPayload);
                    if (!String.IsNullOrEmpty(verificationLogText))
                    {
                        KuduAssert.VerifyLogOutput(appManager, results[0].Id, verificationLogText.Trim());
                    }
                });
            }
        }
    }
}
