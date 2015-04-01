using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class GitRepositoryManagementTests
    {
        [Fact]
        public void PushRepoWithMultipleProjectsShouldDeploy()
        {
            // Arrange
            string appName = "PushMultiProjects";
            string verificationText = "Welcome to ASP.NET MVC!";
            using (var repo = Git.Clone("Mvc3AppWithTestProject"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        // Has been disabled for ages. Commenting out attribute to avoid warning. Should probably just delete
        //[Fact(Skip = "Dangerous")]
        public void PushSimpleWapWithInlineCommand()
        {
            // Arrange
            string appName = "PushSimpleWapWithInlineCommand";

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = msbuild SimpleWebApplication/SimpleWebApplication.csproj /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""%DEPLOYMENT_TARGET%"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Debug;SolutionDir=""%DEPLOYMENT_SOURCE%""");
                Git.Commit(repo.PhysicalPath, "Custom build command added");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "DEBUG");
                });
            }
        }

        // Has been disabled for ages. Commenting out attribute to avoid warning. Should probably just delete
        //[Fact(Skip = "Dangerous")]
        public void PushSimpleWapWithCustomDeploymentScript()
        {
            // Arrange
            string appName = "WapWithCustomDeploymentScript";

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = deploy.cmd");
                Git.Commit(repo.PhysicalPath, "Custom build script added");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "DEBUG");
                });
            }
        }

        [Fact]
        public void PushSimpleWapWithFailingCustomDeploymentScript()
        {
            // Arrange
            string appName = "WapWithFailingCustomDeploymentScript";

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = deploy.cmd");
                repo.WriteFile("deploy.cmd", "bogus");
                Git.Commit(repo.PhysicalPath, "Updated the deploy file.");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, ">bogus", "'bogus' is not recognized as an internal or external command");
                });
            }
        }

        [Fact]
        public async Task CommandSettingOverridesDotDeploymentFile()
        {
            // Arrange
            string appName = "CommandSettingOverridesDotDeploymentFile";

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = deploy.cmd
project = myproject");
                repo.WriteFile("deploy.cmd", "bogus");
                Git.Commit(repo.PhysicalPath, "Updated the deploy file.");

                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    // Act
                    await appManager.SettingsManager.SetValue("COMMAND", "echo test from CommandSettingOverridesDotDeploymentFile & set project");

                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "test from CommandSettingOverridesDotDeploymentFile", "myproject");
                    KuduAssert.VerifyLogOutputWithUnexpected(appManager, results[0].Id, ">bogus", "'bogus' is not recognized as an internal or external command");
                });
            }
        }

        [Fact]
        public void WarningsAsErrors()
        {
            // Arrange
            string appName = "WarningsAsErrors";

            using (var repo = Git.Clone("WarningsAsErrors"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Null(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "Warning as Error: The variable 'x' is declared but never used");
                    Assert.True(deployResult.GitTrace.Contains("Warning as Error: The variable 'x' is declared but never used"));
                    Assert.True(deployResult.GitTrace.Contains("Error - Changes committed to remote repository but deployment to website failed"));
                });
            }
        }

        [Fact]
        public void PushRepoWithProjectAndNoSolutionShouldDeploy()
        {
            // Arrange
            string appName = "PushRepoWithProjNoSln";
            string verificationText = "Kudu Deployment Testing: Mvc3Application_NoSolution";

            using (var repo = Git.Clone("Mvc3Application_NoSolution"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
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
            string appName = "PushNoDeployableProj";

            using (var repo = Git.Clone("NoDeployableProjects"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
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
            string appName = "WapBuildsRelease";

            using (var repo = Git.Clone("ConditionalCompilation"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, null, "RELEASE MODE", "Context.IsDebuggingEnabled: False");
                });
            }
        }

        [Fact]
        public void WebsiteWithIISExpressWorks()
        {
            // Arrange
            string appName = "WebsiteWithIISExpressWorks";

            using (var repo = Git.Clone("waws"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Home");
                });
            }
        }

        [Fact]
        public void PushAppChangesShouldTriggerBuild()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushAppChanges";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    Git.Revert(repo.PhysicalPath);
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        // This test was only interesting when we used signalR (routing issue), which we don't anymore
        //[Fact]
        public void AppNameWithSignalRWorks()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "signalroverflow";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = "DeletesToRepoForWaps";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\AccountController.cs");
                    string projectPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Mvc3Application.csproj");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.OK);

                    File.Delete(deletePath);
                    File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace(@"<Compile Include=""Controllers\AccountController.cs"" />", ""));
                    Git.Commit(repo.PhysicalPath, "Deleted the filez");
                    appManager.GitDeploy(repo.PhysicalPath);
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
            string appName = "PushOverwriteModified";
            string verificationText = "The base color for this template is #5c87b2";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string path = @"Content/Site.css";
                    string url = appManager.SiteUrl + path;

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    KuduAssert.VerifyUrl(url, verificationText);

                    appManager.VfsWebRootManager.WriteAllText(path, "Hello world!");

                    // Sleep a little since it's a remote call
                    Thread.Sleep(500);

                    KuduAssert.VerifyUrl(url, "Hello world!");

                    // Make an unrelated change (newline to the end of web.config)
                    repo.AppendFile(@"Mvc3Application\Web.config", "\n");

                    Git.Commit(repo.PhysicalPath, "This is a test");

                    appManager.GitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(url, verificationText);
                });
            }
        }

        [Fact]
        public void GoingBackInTimeShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "GoBackOverwriteModified";
            string verificationText = "The base color for this template is #5c87b2";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string id = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    string url = appManager.SiteUrl + "/Content/Site.css";
                    // Act
                    appManager.GitDeploy(repositoryName);

                    KuduAssert.VerifyUrl(url, verificationText);

                    repo.AppendFile(@"Mvc3Application\Content\Site.css", "Say Whattttt!");

                    // Make a small changes and commit them to the local repo
                    Git.Commit(repositoryName, "This is a small changes");

                    // Push those changes
                    appManager.GitDeploy(repositoryName);

                    // Make a server site change and verify it shows up
                    appManager.VfsWebRootManager.WriteAllText("Content/Site.css", "Hello world!");

                    Thread.Sleep(500);

                    KuduAssert.VerifyUrl(url, "Hello world!");

                    // Now go back in time
                    appManager.DeploymentManager.DeployAsync(id).Wait();

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(url, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string appName = "DeleteForNonWaps";
            using (var repo = Git.Clone("Bakery"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Content");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Content/Site.css", statusCode: HttpStatusCode.OK);

                    Directory.Delete(deletePath, recursive: true);
                    Git.Commit(repo.PhysicalPath, "Deleted all styles");

                    appManager.GitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Content/Site.css", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void FirstPushDeletesHostingStartHtmlFile()
        {
            string appName = "FirstPushDelPrior";
            using (var repo = Git.Clone("HelloWorld"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string url = appManager.SiteUrl + "hostingstart.html";
                    KuduAssert.VerifyUrl(url, "<h1>This web site has been successfully created</h1>");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

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
            string appName = "PushToNonMaster";
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath, "test", "test");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(0, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, KuduAssert.DefaultPageContent);
                });
            }
        }

        [Fact]
        public void PushingConfiguredBranch()
        {
            string repositoryName = "PushingConfiguredBranch";
            string appName = "PushingConfiguredBranch";
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                Git.CheckOut(repo.PhysicalPath, "foo/bar");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Set the branch to test and push to that branch
                    appManager.SettingsManager.SetValue("branch", "foo/bar").Wait();
                    appManager.GitDeploy(repo.PhysicalPath, "foo/bar", "foo/bar");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "foo/bar branch");

                    // Now push master, but without changing the deploy branch
                    appManager.GitDeploy(repo.PhysicalPath, "master", "master");
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Here, no new deployment should have happened
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "foo/bar branch");

                    // Now change deploy branch to master and do a 'Deploy Latest' (i.e. no id)
                    appManager.SettingsManager.SetValue("branch", "master").Wait();
                    appManager.DeploymentManager.DeployAsync(id: null).Wait();
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Now, we should have a second deployment with master bit
                    Assert.Equal(2, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Master branch");
                });
            }
        }

        [Fact]
        public void PushingNonMasterBranchToMasterBranchShouldDeploy()
        {
            string repositoryName = "PushingNonMasterBranchToMasterBranchShouldDeploy";
            string appName = "PushNonMasterToMaster";
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath, "test", "master");
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
            string appName = "CloneEmptyAndPush";

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var repo = Git.Clone(repositoryName, appManager.GitUrl))
                {
                    // Add a file
                    repo.WriteFile("hello.txt", "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");
                }
            });
        }

        [Fact]
        public void PushThenClone()
        {
            string repositoryName = "Bakery";
            string appName = "PushThenClone";

            using (var repo = Git.Clone(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    using (var clonedRepo = Git.Clone(repositoryName, appManager.GitUrl))
                    {
                        Assert.True(clonedRepo.FileExists("Default.cshtml"));
                    }
                });
            }
        }

        [Fact]
        public void CloneFromNewRepoShouldHaveBeEmpty()
        {
            string repositoryName = "CloneFromNewRepoShouldHaveFile";
            string appName = "CloneNew";

            ApplicationManager.Run(appName, appManager =>
            {
                using (var repo = Git.Clone(repositoryName, appManager.GitUrl))
                {
                    Assert.False(repo.FileExists("index.html"));
                }
            });
        }

        [Fact]
        public void ClonesInParallel()
        {
            string appName = "ClonesInParallel";

            ApplicationManager.Run(appName, appManager =>
            {
                var t1 = Task.Factory.StartNew(() => DoClone("PClone1", appManager));
                var t2 = Task.Factory.StartNew(() => DoClone("PClone2", appManager));
                var t3 = Task.Factory.StartNew(() => DoClone("PClone3", appManager));
                Task.WaitAll(t1, t2, t3);
            });
        }

        private static void DoClone(string repositoryName, ApplicationManager appManager)
        {
            using (var repo = Git.Clone(repositoryName, appManager.GitUrl))
            {
                Assert.False(repo.FileExists("index.html"));
            }
        }

        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string appName = "GoBackDeployOld";
            using (var repo = Git.Clone("HelloWorld"))
            {
                string originalCommitId = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Deploy the app
                    appManager.GitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);

                    // Add a file
                    File.WriteAllText(Path.Combine(repo.PhysicalPath, "hello.txt"), "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    // Deploy those changes
                    appManager.GitDeploy(repo.PhysicalPath);

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");

                    // Go back to the first deployment
                    appManager.DeploymentManager.DeployAsync(originalCommitId).Wait();

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    KuduAssert.VerifyUrl(helloUrl, statusCode: HttpStatusCode.NotFound);
                    Assert.Equal(2, results.Count);
                });
            }
        }

        [Fact]
        public void NpmSiteInstallsPackages()
        {
            string appName = "NpmInstallsPkg";

            using (var repo = Git.Clone("NpmSite"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(appManager.VfsWebRootManager.Exists("node_modules/express"));
                });
            }
        }

        [Fact]
        public void FailedNpmFailsDeployment()
        {
            string appName = "FailedNpm";

            using (var repo = Git.Clone("NpmSite"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Replace the express dependency with something that doesn't exist
                    repo.Replace("package.json", "express", "MadeUpKuduPackage");
                    Git.Commit(repo.PhysicalPath, "Added fake package to package.json");
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "'MadeUpKuduPackage' is not in the npm registry.");
                });
            }
        }

        [Fact]
        public void CustomNodeScript()
        {
            // Arrange
            string repositoryName = "VersionPinnedNodeJsAppCustom";
            string appName = "VersionPinnedNodeJsAppCustom";

            using (var repo = Git.Clone(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "v0.8.2");
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "custom deployment success");
                });
            }
        }

        [Fact]
        public void NodeHelloWorldNoConfig()
        {
            string appName = "NodeConfig";

            using (var repo = Git.Clone("NodeHelloWorldNoConfig"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    // Make sure a web.config file got created at deployment time
                    Assert.True(appManager.VfsWebRootManager.Exists("web.config"));

                    // Make sure the web.config file doesn't exist on the repository after the deployment finished
                    Assert.False(appManager.VfsManager.Exists("site\\repository\\web.config"));
                });
            }
        }

        [Fact]
        public void NodeWithSolutionsUnderNodeModulesShouldBeDeployed()
        {
            string appName = "NodeWithSolutionsUnderNodeModulesShouldBeDeployed";

            using (var repo = Git.Clone("NodeWithSolutions"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    var log = GetLog(appManager, results[0].Id);
                    Assert.Contains("Handling node.js deployment", log);
                });
            }
        }

        [Fact]
        public void RedeployNodeSite()
        {
            string appName = "RedeployNodeSite";

            using (var repo = Git.Clone("NodeHelloWorldNoConfig"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    var log1 = GetLog(appManager, results[0].Id);
                    Assert.Contains("Using the following command to generate deployment script", log1);

                    string id = results[0].Id;

                    repo.Replace("server.js", "world", "world2");
                    Git.Commit(repo.PhysicalPath, "Made a small change");

                    appManager.GitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    appManager.DeploymentManager.DeployAsync(id).Wait();
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    var log2 = GetLog(appManager, results[0].Id);
                    var log3 = GetLog(appManager, results[1].Id);
                    Assert.Contains("Using cached version of deployment script", log2);
                    Assert.Contains("Using cached version of deployment script", log3);
                });
            }
        }

        [Fact]
        public void GetResults()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "RsltMaxItemXcldFailed";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string homeControllerPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\HomeController.cs");

                    // Act, Initial Commit
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change1");

                    // Act, Change 2
                    File.WriteAllText(homeControllerPath, File.ReadAllText(homeControllerPath).Replace(" - Change1", " - Change2"));
                    Git.Commit(repo.PhysicalPath, "Change 2!");
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change2");

                    // Act, Invalid Change build-break change and commit
                    File.WriteAllText(homeControllerPath, File.ReadAllText(homeControllerPath).Replace("Index()", "Index;"));
                    Git.Commit(repo.PhysicalPath, "Invalid Change!");
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(3, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Assert.Equal(DeployStatus.Success, results[2].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change2");
                });
            }
        }

        [Fact]
        public void RepoWithPublicSubModuleTest()
        {
            // Arrange
            string repositoryName = "RepoWithPublicSubModule";
            string appName = "RepoWithPublicSubModule";
            string cloneUrl = "https://github.com/KuduApps/RepoWithPublicSubModule.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "default.htm", "Hello from RepoWithPublicSubModule!");
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "PublicSubModule/default.htm", "Hello from PublicSubModule!");
                });
            }
        }

        [Fact]
        public void RepoWithPrivateSubModuleTest()
        {
            // Arrange
            string appName = "RepoWithPrivateSubModule";
            string id_rsa;

            IDictionary<string, string> environmentVariables = SshHelper.PrepareSSHEnv(out id_rsa);
            if (String.IsNullOrEmpty(id_rsa))
            {
                return;
            }

            using (var repo = Git.Clone("RepoWithPrivateSubModule", environments: environmentVariables))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.SSHKeyManager.SetPrivateKey(id_rsa);

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "default.htm", "Hello from RepoWithPrivateSubModule!");
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "PrivateSubModule/default.htm", "Hello from PrivateSubModule!");
                });
            }
        }

        [Fact]
        public void HangProcessTest()
        {
            // Arrange
            string appName = "HangProcess";

            using (var repo = Git.Clone("HangProcess"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    // Set IdleTimeout to 10s meaning there must be activity every 10s
                    // Otherwise process and its child will be terminated
                    appManager.SettingsManager.SetValue(SettingsKeys.CommandIdleTimeout, "10").Wait();

                    // This HangProcess repo spew out activity at 2s, 4s, 6s and 30s respectively
                    // we should receive the one < 10s and terminate otherwise.
                    GitDeploymentResult result = appManager.GitDeploy(repo.PhysicalPath, retries: 1);
                    string trace = result.GitTrace;

                    Assert.Contains("remote: Sleep(2000)", trace);
                    Assert.Contains("remote: Sleep(4000)", trace);
                    Assert.Contains("remote: Sleep(6000)", trace);
                    Assert.DoesNotContain("remote: Sleep(60000)", trace);
                    Assert.Contains("remote: Command 'starter.cmd simplesleep.exe ...' aborted due to no output and CPU activity for", trace);
                    Assert.False(Process.GetProcesses().Any(p => p.ProcessName.Equals("simplesleep", StringComparison.OrdinalIgnoreCase)), "SimpleSleep should have been terminated!");
                });
            }
        }

        [Fact]
        public void WaitForUserInputProcessTest()
        {
            // Arrange
            string appName = "WaitForUserInput";

            using (var repo = Git.Clone("WaitForUserInput"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    // Set IdleTimeout to 10s meaning there must be activity every 10s
                    // Otherwise process and its child will be terminated
                    appManager.SettingsManager.SetValue(SettingsKeys.CommandIdleTimeout, "10").Wait();

                    // This WaitForUserInput do set /P waiting for input
                    GitDeploymentResult result = appManager.GitDeploy(repo.PhysicalPath, retries: 1);
                    string trace = result.GitTrace;

                    Assert.Contains("remote: Insert your input:", trace);
                    Assert.Contains("remote: Command 'starter.cmd waitforinput.ba ...' aborted due to no output and CPU activity for", trace);
                });
            }
        }

        [Fact]
        public void HookForbiddenForSomeScmTypes()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://github.com"", ""commits"": [ { ""author"": ""davidebbo"", ""branch"": ""master"", ""files"": [ { ""file"": ""Mvc3Application/Views/Home/Index.cshtml"", ""type"": ""modified"" } ], ""message"": ""Blah2\n"", ""node"": ""e550351c5188"", ""parents"": [ ""297fcc65308c"" ], ""raw_author"": ""davidebbo <david.ebbo@microsoft.com>"", ""raw_node"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-09-20 03:11:20"", ""utctimestamp"": ""2012-09-20 01:11:20+00:00"" } ], ""repository"": { ""absolute_url"": ""/KuduApps/SimpleWebApplication"", ""fork"": false, ""is_private"": false, ""name"": ""Mvc3Application"", ""owner"": ""davidebbo"", ""scm"": ""git"", ""slug"": ""mvc3application"", ""website"": """" }, ""user"": ""davidebbo"" }";
            string githubPayload = @"{ ""after"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""before"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"",  ""commits"": [ { ""added"": [], ""author"": { ""email"": ""prkrishn@hotmail.com"", ""name"": ""Pranav K"", ""username"": ""pranavkm"" }, ""id"": ""43acf30efa8339103e2bed5c6da1379614b00572"", ""message"": ""Changes from master again"", ""modified"": [ ""Hello.txt"" ], ""timestamp"": ""2012-12-17T17:32:15-08:00"" } ], ""compare"": ""https://github.com/KuduApps/GitHookTest/compare/7e2a599e2d28...7e2a599e2d28"", ""created"": false, ""deleted"": false, ""forced"": false, ""head_commit"": { ""added"": [ "".gitignore"", ""SimpleWebApplication.sln"", ""SimpleWebApplication/About.aspx"", ""SimpleWebApplication/About.aspx.cs"", ""SimpleWebApplication/About.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx"", ""SimpleWebApplication/Account/ChangePassword.aspx.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.designer.cs"", ""SimpleWebApplication/Account/Login.aspx"", ""SimpleWebApplication/Account/Login.aspx.cs"", ""SimpleWebApplication/Account/Login.aspx.designer.cs"", ""SimpleWebApplication/Account/Register.aspx"", ""SimpleWebApplication/Account/Register.aspx.cs"", ""SimpleWebApplication/Account/Register.aspx.designer.cs"", ""SimpleWebApplication/Account/Web.config"", ""SimpleWebApplication/Default.aspx"", ""SimpleWebApplication/Default.aspx.cs"", ""SimpleWebApplication/Default.aspx.designer.cs"", ""SimpleWebApplication/Global.asax"", ""SimpleWebApplication/Global.asax.cs"", ""SimpleWebApplication/Properties/AssemblyInfo.cs"", ""SimpleWebApplication/Scripts/jquery-1.4.1-vsdoc.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.min.js"", ""SimpleWebApplication/SimpleWebApplication.csproj"", ""SimpleWebApplication/Site.Master"", ""SimpleWebApplication/Site.Master.cs"", ""SimpleWebApplication/Site.Master.designer.cs"", ""SimpleWebApplication/Styles/Site.css"", ""SimpleWebApplication/Web.Debug.config"", ""SimpleWebApplication/Web.Release.config"", ""SimpleWebApplication/Web.config"" ], ""author"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""committer"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""distinct"": false, ""id"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""message"": ""Initial"", ""modified"": [], ""removed"": [], ""timestamp"": ""2011-11-21T23:07:42-08:00"", ""url"": ""https://github.com/KuduApps/GitHookTest/commit/7e2a599e2d28665047ec347ab36731c905c95e8b"" }, ""pusher"": { ""name"": ""none"" }, ""ref"": ""refs/heads/master"", ""repository"": { ""created_at"": ""2012-06-28T00:07:55-07:00"", ""description"": """", ""fork"": false, ""forks"": 1, ""has_downloads"": true, ""has_issues"": true, ""has_wiki"": true, ""language"": ""ASP"", ""name"": ""GitHookTest"", ""open_issues"": 0, ""organization"": ""KuduApps"", ""owner"": { ""email"": ""kuduapps@hotmail.com"", ""name"": ""KuduApps"" }, ""private"": false, ""pushed_at"": ""2012-06-28T00:11:48-07:00"", ""size"": 188, ""url"": ""https://github.com/KuduApps/SimpleWebApplication"", ""watchers"": 1 } }";

            // Arrange
            string appName = "ScmTypeTest";
            ApplicationManager.Run(appName, appManager =>
            {
                // Default value test
                try
                {
                    // for private kudu, the setting is LocalGit
                    string value = appManager.SettingsManager.GetValue(SettingsKeys.ScmType).Result;
                    Assert.Equal("LocalGit", value);
                }
                catch (AggregateException ex)
                {
                    // for public kudu, the setting does not exist
                    var notFoundException = ex.InnerExceptions.OfType<HttpUnsuccessfulRequestException>().First();
                    Assert.Equal(HttpStatusCode.NotFound, notFoundException.ResponseMessage.StatusCode);
                }

                // Make sure it's disabled for None and Tfs* ScmTypes
                EnsureDeployDisabled(appManager, ScmType.None, null, githubPayload);
                EnsureDeployDisabled(appManager, ScmType.Tfs, "Bitbucket.org", bitbucketPayload);
                EnsureDeployDisabled(appManager, ScmType.TfsGit, "Bitbucket.org", bitbucketPayload);
            });
        }

        private void EnsureDeployDisabled(ApplicationManager appManager, string scmType, string userAgent, string payload)
        {
            appManager.SettingsManager.SetValue(SettingsKeys.ScmType, scmType).Wait();
            HttpClient client = CreateClient(appManager);
            var post = new Dictionary<string, string>
            {
                { "payload", payload }
            };
            if (userAgent != null)
            {
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            }
            HttpResponseMessage response = client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result;
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
                                          KuduAssert.DefaultPageContent,
                                          DeployStatus.Failed);
        }

        [Fact]
        public void SpecificDeploymentConfigurationForNonDeployableProject()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForNonDeployableProject",
                                          "MvcApplicationRemovedFromSolution.Tests",
                                          KuduAssert.DefaultPageContent,
                                          DeployStatus.Failed,
                                          "is not a deployable project");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForDirectoryThatDoesNotExist()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForDirectoryThatDoesNotExist",
                                          "IDoNotExist",
                                          KuduAssert.DefaultPageContent,
                                          DeployStatus.Failed);
        }

        private static HttpClient CreateClient(ApplicationManager appManager)
        {
            HttpClientHandler handler = HttpClientHelper.CreateClientHandler(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(appManager.ServiceUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        private static string GetLog(ApplicationManager appManager, string resultId)
        {
            var entries = appManager.DeploymentManager.GetLogEntriesAsync(resultId).Result;
            var allDetails = entries.Where(e => e.DetailsUrl != null)
                                    .SelectMany(e => appManager.DeploymentManager.GetLogEntryDetailsAsync(resultId, e.Id).Result);
            var allEntries = entries.Concat(allDetails);
            return String.Join("\n", allEntries.Select(entry => entry.Message));
        }

        private void VerifyDeploymentConfiguration(string siteName, string targetProject, string expectedText, DeployStatus expectedStatus = DeployStatus.Success, string expectedLog = null)
        {
            string name = siteName;
            string cloneUrl = "https://github.com/KuduApps/SpecificDeploymentConfiguration.git";
            using (var repo = Git.Clone(name, cloneUrl))
            {
                ApplicationManager.Run(name, appManager =>
                {
                    string deploymentFile = Path.Combine(repo.PhysicalPath, @".deployment");
                    File.WriteAllText(deploymentFile, String.Format(@"[config]
project = {0}
", targetProject));
                    Git.Commit(repo.PhysicalPath, "Updated configuration " + Guid.NewGuid());

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(expectedStatus, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, expectedText);
                    if (!String.IsNullOrEmpty(expectedLog))
                    {
                        KuduAssert.VerifyLogOutput(appManager, results[0].Id, expectedLog);
                    }
                });
            }
        }
    }
}
