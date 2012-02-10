using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
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
            string appName = "PushSimpleRepoShouldDeploy";
            string verificationText = "Welcome to Fourth Coffee!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    var results = appManager.DeploymentManager.GetResults().ToList();
                    var deployedFiles = new HashSet<string>(appManager.DeploymentManager.GetManifest(repo.CurrentId), StringComparer.OrdinalIgnoreCase);

                    // Assert
                    Assert.True(deployedFiles.Contains("Default.cshtml"));
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushRepoWithMultipleProjectsShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushRepoWithMultipleProjectsShouldDeploy";
            string verificationText = "Welcome to ASP.NET MVC! - Change1";
            using (Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushRepoWithProjectAndNoSolutionShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application_NoSolution";
            string appName = "PushRepoWithProjectAndNoSolutionShouldDeploy";
            string verificationText = "Kudu Deployment Testing: Mvc3Application_NoSolution";

            using (Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushAppChangesShouldTriggerBuild()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushAppChangesShouldTriggerBuild";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    Git.Revert(repositoryName);
                    appManager.GitDeploy(repositoryName);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = "DeletesToRepositoryArePropagatedForWaps";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\AccountController.cs");
                    string projectPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Mvc3Application.csproj");

                    // Act
                    appManager.GitDeploy(repositoryName);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.OK);

                    File.Delete(deletePath);
                    File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace(@"<Compile Include=""Controllers\AccountController.cs"" />", ""));
                    Git.Commit(repositoryName, "Deleted the filez");
                    appManager.GitDeploy(repositoryName);
                    results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Verify(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushShouldOverwriteModifiedFilesInRepo";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);

                    Verify(appManager.SiteUrl, verificationText);

                    appManager.ProjectSystem.WriteAllText("Views/Home/Index.cshtml", "Hello world!");

                    Verify(appManager.SiteUrl, "Hello world!");

                    // Make an unrelated change (newline to the end of web.config)
                    repo.AppendFile(@"Mvc3Application\Web.config", "\n");

                    Git.Commit(repositoryName, "This is a test");

                    appManager.GitDeploy(repositoryName);

                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void GoingBackInTimeShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "GoingBackInTimeShouldOverwriteModifiedFilesInRepo";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string id = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);

                    Verify(appManager.SiteUrl, verificationText);

                    repo.AppendFile(@"Mvc3Application\Views\Home\Index.cshtml", "Say Whattttt!");

                    // Make a small changes and commit them to the local repo
                    Git.Commit(repositoryName, "This is a small changes"); 

                    // Push those changes
                    appManager.GitDeploy(repositoryName);

                    // Make a server site change and verify it shows up
                    appManager.ProjectSystem.WriteAllText("Views/Home/Index.cshtml", "Hello world!");

                    Verify(appManager.SiteUrl, "Hello world!");

                    // Now go back in time
                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        appManager.DeploymentManager.Deploy(id);
                    });


                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string repositoryName = "Bakery10";
            string appName = "DeletesToRepositoryArePropagatedForNonWaps";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Styles");

                    // Act
                    appManager.GitDeploy(repositoryName);

                    // Assert
                    var results = appManager.DeploymentManager.GetResults().ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl + "Styles/Site.css", statusCode: HttpStatusCode.OK);


                    Directory.Delete(deletePath, recursive: true);
                    Git.Commit(repositoryName, "Deleted all styles");

                    appManager.GitDeploy(repositoryName);
                    results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Verify(appManager.SiteUrl + "Styles/Site.css", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void FirstPushDeletesPriorContent()
        {
            string repositoryName = "Bakery10";
            string appName = "FirstPushDeletesPriorContent";
            using (Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.ProjectSystem.WriteAllText("foo.txt", "This is a test file");
                    string url = appManager.SiteUrl + "/foo.txt";

                    Verify(url, "This is a test file");

                    // Act
                    appManager.GitDeploy(repositoryName);

                    // Assert
                    var results = appManager.DeploymentManager.GetResults().ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl);
                    Verify(url, statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void DeployingNonMasterBranchNoOps()
        {
            string repositoryName = "DeployingNonMasterBranchNoOps";
            string appName = "DeployingNonMasterBranchNoOps";
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (Git.Clone(repositoryName, cloneUrl))
            {
                Git.CheckOut(repositoryName, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName, "test", "test");
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(0, results.Count);
                    Verify(appManager.SiteUrl, "The web site is under construction");
                });
            }
        }

        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string repositoryName = "Bakery10";
            string appName = "GoingBackInTimeDeploysOldFiles";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string originalCommitId = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Deploy the app
                    appManager.GitDeploy(repositoryName);

                    var results = appManager.DeploymentManager.GetResults().ToList();
                    Assert.Equal(1, results.Count);
                    Verify(appManager.SiteUrl);

                    // Add a file
                    File.WriteAllText(Path.Combine(repo.PhysicalPath, "hello.txt"), "Wow");
                    Git.Commit(repositoryName, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    // Deploy those changes
                    appManager.GitDeploy(repositoryName);

                    results = appManager.DeploymentManager.GetResults().ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Verify(helloUrl, "Wow");

                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        // Go back to the first deployment
                        appManager.DeploymentManager.Deploy(originalCommitId);
                    });

                    results = appManager.DeploymentManager.GetResults().ToList();
                    Verify(helloUrl, statusCode: HttpStatusCode.NotFound);
                    Assert.Equal(2, results.Count);
                });
            }
        }

        [Fact]
        public void SpecificDeploymentConfiguration()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfiguration",
                                          "WebApplication1",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForProjectFile()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForProjectFile",
                                          "WebApplication1/WebApplication1.csproj",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsite()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebsite",
                                          "WebSite1",
                                          "This is a website!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsiteWithSlash()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebsiteWithSlash",
                                          "/WebSite1",
                                          "This is a website!");
        }

        private void VerifyDeploymentConfiguration(string name, string targetProject, string expectedText)
        {
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
                    appManager.GitDeploy(name);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Verify(appManager.SiteUrl, expectedText);
                });
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
    }
}
