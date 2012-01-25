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
    public class GitDeploymentTests
    {
        [Fact]
        public void PushSimpleRepoShouldDeploy()
        {
            // Arrange
            string repositoryName = "Bakery2";
            string appName = "PushSimpleRepoShouldDeploy";
            string verificationText = "Welcome to Fourth Coffee!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();
                    var deployedFiles = new HashSet<string>(appManager.DeploymentManager.GetManifest(repo.CurrentId), StringComparer.OrdinalIgnoreCase);

                    // Assert
                    Assert.True(deployedFiles.Contains("Default.cshtml"));
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains(verificationText));
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
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains(verificationText));
                });
            }
        }

        [Fact(Skip = "Issue #11")]
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
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains(verificationText));
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
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains(verificationText));
                });
            }
        }

        [Fact]
        public void NodeExpressApplication()
        {
            string repositoryName = "Express-Template";
            string cloneUrl = "https://github.com/davidebbo/Express-Template.git";

            using (Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(repositoryName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);

                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = "DeletesToRepositoryArePropagatedForWaps";
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Scripts\jquery-1.5.1.js");
                    string projectPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Mvc3Application.csproj");

                    // Act
                    appManager.GitDeploy(repositoryName);
                    File.Delete(deletePath);
                    File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace(@"<Content Include=""Scripts\jquery-1.5.1.js"" />", ""));
                    Git.Commit(repositoryName, "Deleted all scripts");
                    appManager.GitDeploy(repositoryName);
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains(verificationText));
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string repositoryName = "Bakery2";
            string appName = "DeletesToRepositoryArePropagatedForNonWaps";
            string verificationText = "Welcome to Fourth Coffee!";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Styles");

                    // Act
                    appManager.GitDeploy(repositoryName);
                    Directory.Delete(deletePath, recursive: true);
                    Git.Commit(repositoryName, "Deleted all styles");
                    appManager.GitDeploy(repositoryName);
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains(verificationText));
                });
            }
        }

        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string repositoryName = "Bakery2";
            string appName = "GoingBackInTimeDeploysOldFiles";
            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string originalCommitId = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Deploy the app
                    appManager.GitDeploy(repositoryName);

                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    // Add a file
                    File.WriteAllText(Path.Combine(repo.PhysicalPath, "hello.txt"), "Wow");
                    Git.Commit(repositoryName, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    // Deploy those changes
                    appManager.GitDeploy(repositoryName);

                    response = GetResponseBody(helloUrl);
                    results = appManager.DeploymentManager.GetResults().ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Assert.Equal("Wow", response);

                    appManager.DeploymentManager.WaitForDeployment(() =>
                    {
                        // Go back to the first deployment
                        appManager.DeploymentManager.Deploy(originalCommitId);
                    });

                    results = appManager.DeploymentManager.GetResults().ToList();

                    Assert.Equal(HttpStatusCode.NotFound, GetResponse(helloUrl).StatusCode);
                    Assert.Equal(2, results.Count);
                });
            }
        }

        [Fact]
        public void ProjectWithoutSolution()
        {
            string repositoryName = "ProjectWithNoSolution";
            string appName = "ProjectWithNoSolution";
            string cloneUrl = "https://github.com/KuduApps/ProjectWithNoSolution.git";

            using (Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains("Project without solution"));
                });
            }
        }

        [Fact]
        public void HiddenFilesAndFoldersAreDeployed()
        {
            string repositoryName = "HiddenFoldersAndFiles";
            string appName = "HiddenFilesAndFoldersAreDeployed";
            string cloneUrl = "https://github.com/KuduApps/HiddenFoldersAndFiles.git";
            using (Git.Clone(repositoryName, cloneUrl))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repositoryName);
                    string response = GetResponseBody(appManager.SiteUrl);
                    var results = appManager.DeploymentManager.GetResults().ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(response.Contains("Hello World"));
                });
            }
        }

        private string GetResponseBody(string url)
        {
            return GetResponse(url).EnsureSuccessStatusCode().Content.ReadAsStringAsync().Result;
        }

        private HttpResponseMessage GetResponse(string url)
        {
            var client = new HttpClient();
            return client.GetAsync(url).Result;
        }
    }
}
