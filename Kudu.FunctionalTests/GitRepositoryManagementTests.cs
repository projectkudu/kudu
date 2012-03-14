using System;
using System.IO;
using System.Linq;
using System.Net;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class GitRepositoryManagementTests
    {
        [Fact]
        public void PushSimpleRepoShouldDeploy()
        {
            // Arrange
            string repositoryName = "Bakery10";
            string appName = KuduUtils.GetRandomWebsiteName("PushSimpleRepo");
            string verificationText = "Welcome to Fourth Coffee!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushRepoWithMultipleProjectsShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName =  KuduUtils.GetRandomWebsiteName("PushMultiProjects");
            string verificationText = "Welcome to ASP.NET MVC! - Change1";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void WarningsAsErrors()
        {
            // Arrange
            string repositoryName = "WarningsAsErrors";
            string appName = KuduUtils.GetRandomWebsiteName("WarningsAsErrors");
            string cloneUrl = "https://github.com/KuduApps/WarningsAsErrors.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Null(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "Warning as Error: The variable 'x' is declared but never used");
                });
            }
        }

        [Fact]
        public void PushRepoWithProjectAndNoSolutionShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application_NoSolution";
            string appName = KuduUtils.GetRandomWebsiteName("PushRepoWithProjNoSln");
            string verificationText = "Kudu Deployment Testing: Mvc3Application_NoSolution";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushRepositoryWithNoDeployableProjectsTreatsAsWebsite()
        {
            // Arrange
            string repositoryName = "PushRepositoryWithNoDeployableProjectsTreatsAsWebsite";
            string appName = KuduUtils.GetRandomWebsiteName("PushNoDeployableProj");
            string cloneUrl = "https://github.com/KuduApps/NoDeployableProjects.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "no deployable projects. Deploying files instead");
                });
            }
        }

        [Fact]
        public void WapBuildsReleaseMode()
        {
            // Arrange
            string repositoryName = "WapBuildsReleaseMode";
            string appName = KuduUtils.GetRandomWebsiteName("WapBuildsRelease");
            string cloneUrl = "https://github.com/KuduApps/ConditionalCompilation.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, null, "RELEASE MODE", "Context.IsDebuggingEnabled: False");
                });
            }
        }

        [Fact]
        public void PushAppChangesShouldTriggerBuild()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("PushAppChanges");
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    Git.Revert(repo.PhysicalPath);
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("DeletesToRepoForWaps");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\AccountController.cs");
                    string projectPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Mvc3Application.csproj");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.OK);

                    File.Delete(deletePath);
                    File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace(@"<Compile Include=""Controllers\AccountController.cs"" />", ""));
                    Git.Commit(repo.PhysicalPath, "Deleted the filez");
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("PushOverwriteModified");
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);

                    appManager.ProjectSystem.WriteAllText("Views/Home/Index.cshtml", "Hello world!");

                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello world!");

                    // Make an unrelated change (newline to the end of web.config)
                    repo.AppendFile(@"Mvc3Application\Web.config", "\n");

                    Git.Commit(repo.PhysicalPath, "This is a test");

                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void GoingBackInTimeShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("GoBackOverwriteModified");
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string id = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repositoryName);

                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);

                    repo.AppendFile(@"Mvc3Application\Views\Home\Index.cshtml", "Say Whattttt!");

                    // Make a small changes and commit them to the local repo
                    Git.Commit(repositoryName, "This is a small changes");

                    // Push those changes
                    appManager.AssertGitDeploy(repositoryName);

                    // Make a server site change and verify it shows up
                    appManager.ProjectSystem.WriteAllText("Views/Home/Index.cshtml", "Hello world!");

                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello world!");

                    // Now go back in time
                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        appManager.DeploymentManager.DeployAsync(id).Wait();
                    });


                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string repositoryName = "Bakery10";
            string appName = KuduUtils.GetRandomWebsiteName("DeleteForNonWaps");
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Styles");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Styles/Site.css", statusCode: HttpStatusCode.OK);


                    Directory.Delete(deletePath, recursive: true);
                    Git.Commit(repo.PhysicalPath, "Deleted all styles");

                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Styles/Site.css", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void FirstPushDeletesPriorContent()
        {
            string repositoryName = "Bakery10";
            string appName = KuduUtils.GetRandomWebsiteName("FirstPushDelPrior");
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.ProjectSystem.WriteAllText("foo.txt", "This is a test file");
                    string url = appManager.SiteUrl + "/foo.txt";

                    KuduAssert.VerifyUrl(url, "This is a test file");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);
                    KuduAssert.VerifyUrl(url, statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushingToNonMasterBranchNoOps()
        {
            string repositoryName = "PushingToNonMasterBranchNoOps";
            string appName = KuduUtils.GetRandomWebsiteName("PushToNonMaster");
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath, "test", "test");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(0, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "The web site is under construction");
                });
            }
        }

        [Fact]
        public void PushingNonMasterBranchToMasterBranchShouldDeploy()
        {
            string repositoryName = "PushingNonMasterBranchToMasterBranchShouldDeploy";
            string appName = KuduUtils.GetRandomWebsiteName("PushNonMasterToMaster");
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath, "test", "master");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                });
            }
        }

        [Fact]
        public void CloneFromEmptyRepoAndPushShouldDeploy()
        {
            string repositoryName = "CloneFromEmptyRepoAndPushShouldDeploy";
            string appName = KuduUtils.GetRandomWebsiteName("CloneEmptyAndPush");

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var repo = Git.Clone(repositoryName, appManager.GitUrl, createDirectory: true))
                {
                    // Add a file
                    repo.WriteFile("hello.txt", "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");
                }
            });
        }


        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string repositoryName = "Bakery10";
            string appName = KuduUtils.GetRandomWebsiteName("GoBackDeployOld");
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string originalCommitId = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Deploy the app
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);

                    // Add a file
                    File.WriteAllText(Path.Combine(repo.PhysicalPath, "hello.txt"), "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    // Deploy those changes
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");

                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        // Go back to the first deployment
                        appManager.DeploymentManager.DeployAsync(originalCommitId).Wait();
                    });

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    KuduAssert.VerifyUrl(helloUrl, statusCode: HttpStatusCode.NotFound);
                    Assert.Equal(2, results.Count);
                });
            }
        }

        [Fact(Skip = "Known failure, NPM bug https://github.com/isaacs/npm/issues/2190")]
        public void NpmSiteInstallsPackages()
        {
            string repositoryName = "NpmSiteInstallsPackages";
            string appName = KuduUtils.GetRandomWebsiteName("NpmInstallsPkg");
            string cloneUrl = "https://github.com/KuduApps/NpmSite.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    var files = appManager.ProjectSystem.GetProject().Files.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(files.Any(f => f.StartsWith("node_modules/express")));
                });
            }
        }

        [Fact(Skip = "Known failure, NPM bug https://github.com/isaacs/npm/issues/2190")]
        public void FailedNpmFailsDeployment()
        {
            string repositoryName = "FailedNpmFailsDeployment";
            string appName = KuduUtils.GetRandomWebsiteName("FailedNpm");
            string cloneUrl = "https://github.com/KuduApps/NpmSite.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Replace the express dependency with something that doesn't exist
                    repo.Replace("package.json", "express", "MadeUpKuduPackage");
                    Git.Commit(repo.PhysicalPath, "Added fake package to package.json");
                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "'MadeUpKuduPackage' is not in the npm registry.");
                });
            }
        }

        [Fact]
        public void GetResultsWithMaxItemsAndExcludeFailed()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("RsltMaxItemXcldFailed");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string homeControllerPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\HomeController.cs");

                    // Act, Initial Commit
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change1");

                    // Act, Change 2
                    File.WriteAllText(homeControllerPath, File.ReadAllText(homeControllerPath).Replace(" - Change1", " - Change2"));
                    Git.Commit(repo.PhysicalPath, "Change 2!");
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change2");

                    // Act, Invalid Change build-break change and commit
                    File.WriteAllText(homeControllerPath, File.ReadAllText(homeControllerPath).Replace("Index()", "Index;"));
                    Git.Commit(repo.PhysicalPath, "Invalid Change!");
                    appManager.AssertGitDeploy(repo.PhysicalPath);

                    // Assert
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(3, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Assert.Equal(DeployStatus.Success, results[2].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change2");

                    // Test maxItems = 2
                    results = appManager.DeploymentManager.GetResultsAsync(maxItems: 2).Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    // Test excludeFailed = true
                    results = appManager.DeploymentManager.GetResultsAsync(excludeFailed: true).Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    // Test maxItems = 1, excludeFailed = true
                    results = appManager.DeploymentManager.GetResultsAsync(maxItems: 1, excludeFailed: true).Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(results[0].Current);
                });
            }
        }

        [Fact]
        public void SpecificDeploymentConfiguration()
        {
            VerifyDeploymentConfiguration("SpecificDeployConfig",
                                          "WebApplication1",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForProjectFile()
        {
            VerifyDeploymentConfiguration("DeployConfigForProj",
                                          "WebApplication1/WebApplication1.csproj",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsite()
        {
            VerifyDeploymentConfiguration("DeployConfigForWeb",
                                          "WebSite1",
                                          "This is a website!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsiteWithSlash()
        {
            VerifyDeploymentConfiguration("DeployConfigWithSlash",
                                          "/WebSite1",
                                          "This is a website!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsiteNotPartOfSolution()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebsiteNotPartOfSolution",
                                          "WebSiteRemovedFromSolution",
                                          "This web site was removed from solution!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebProjectNotPartOfSolution()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebProjectNotPartOfSolution",
                                          "MvcApplicationRemovedFromSolution",
                                          "This web project was removed from solution!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForNonDeployableProjectFile()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForNonDeployableProjectFile",
                                          "MvcApplicationRemovedFromSolution.Tests/MvcApplicationRemovedFromSolution.Tests.csproj",
                                          "The web site is under construction",
                                          DeployStatus.Failed);
        }

        [Fact]
        public void SpecificDeploymentConfigurationForNonDeployableProject()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForNonDeployableProject",
                                          "MvcApplicationRemovedFromSolution.Tests",
                                          "The web site is under construction",
                                          DeployStatus.Failed);
        }

        [Fact]
        public void SpecificDeploymentConfigurationForDirectoryThatDoesNotExist()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForDirectoryThatDoesNotExist",
                                          "IDoNotExist",
                                          "The web site is under construction",
                                          DeployStatus.Failed);
        }

        private void VerifyDeploymentConfiguration(string siteName, string targetProject, string expectedText, DeployStatus expectedStatus = DeployStatus.Success)
        {
            string name = KuduUtils.GetRandomWebsiteName(siteName);
            string cloneUrl = "https://github.com/KuduApps/SpecificDeploymentConfiguration.git";
            using (var repo = Git.Clone(name, cloneUrl))
            {
                ApplicationManager.Run(name, appManager =>
                {
                    string deploymentFile = Path.Combine(repo.PhysicalPath, @".deployment");
                    File.WriteAllText(deploymentFile, String.Format(@"[config]
project = {0}", targetProject));
                    Git.Commit(name, "Updated configuration");

                    // Act
                    appManager.AssertGitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(expectedStatus, results[0].Status);                    
                    KuduAssert.VerifyUrl(appManager.SiteUrl, expectedText);
                });
            }
        }
    }
}
