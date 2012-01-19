using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl.Git;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Web.Infrastructure;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class GitDeploymentTests : IDisposable
    {
        [Fact]
        public void PushSimpleRepoShouldDeploy()
        {
            // Arrange
            string repositoryName = "Bakery2";
            string appName = "PushSimpleRepoShouldDeploy";
            string verificationText = "Welcome to Fourth Coffee!";
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                // Act
                appManager.GitDeploy(repositoryName);
                string response = GetResponseBody(appManager.SiteUrl);
                var results = appManager.DeploymentManager.GetResults().ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(response.Contains(verificationText));
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact]
        public void PushRepoWithMultipleProjectsShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushRepoWithMultipleProjectsShouldDeploy";
            string verificationText = "Welcome to ASP.NET MVC! - Change1";
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                // Act
                appManager.GitDeploy(repositoryName);
                string response = GetResponseBody(appManager.SiteUrl);
                var results = appManager.DeploymentManager.GetResults().ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(response.Contains(verificationText));
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact(Skip = "Issue #11")]
        public void PushRepoWithProjectAndNoSolutionShouldDeploy()
        {
            // Arrange
            string repositoryName = "Mvc3Application_NoSolution";
            string appName = "PushRepoWithProjectAndNoSolutionShouldDeploy";
            string verificationText = "Kudu Deployment Testing: Mvc3Application_NoSolution";
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                // Act
                appManager.GitDeploy(repositoryName);
                string response = GetResponseBody(appManager.SiteUrl);
                var results = appManager.DeploymentManager.GetResults().ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(response.Contains(verificationText));
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact]
        public void PushAppChangesShouldTriggerBuild()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "PushAppChangesShouldTriggerBuild";
            string verificationText = "Welcome to ASP.NET MVC!";
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
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
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact]
        public void NodeExpressApplication()
        {
            string repositoryName = "Express-Template";
            string cloneUrl = "https://github.com/davidebbo/Express-Template.git";
            string originRepo = Git.Clone(repositoryName, cloneUrl);

            using (var appManager = ApplicationManager.CreateApplication(repositoryName))
            {
                // Act
                appManager.GitDeploy(repositoryName);

                var results = appManager.DeploymentManager.GetResults().ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = "DeletesToRepositoryArePropagatedForWaps";
            string verificationText = "Welcome to ASP.NET MVC!";
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                string deletePath = Path.Combine(originRepo, @"Mvc3Application\Scripts\jquery-1.5.1.js");
                string projectPath = Path.Combine(originRepo, @"Mvc3Application\Mvc3Application.csproj");
                
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
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string repositoryName = "Bakery2";
            string appName = "DeletesToRepositoryArePropagatedForNonWaps";
            string verificationText = "Welcome to Fourth Coffee!";
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                string deletePath = Path.Combine(originRepo, @"Styles");

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
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string repositoryName = "Bakery2";
            string appName = "PushSimpleRepoShouldDeploy";
            string originRepo = Git.CreateLocalRepository(repositoryName);
            var repository = new GitExeRepository(originRepo);
            string originalCommitId = repository.CurrentId;

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                // Deploy the app
                appManager.GitDeploy(repositoryName);

                string response = GetResponseBody(appManager.SiteUrl);
                var results = appManager.DeploymentManager.GetResults().ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);

                // Add a file
                File.WriteAllText(Path.Combine(originRepo, "hello.txt"), "Wow");
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
                Assert.Equal(DeployStatus.Success, results[0].Status);
            }
        }

        private string GetResponseBody(string url)
        {
            return GetResponse(url).EnsureSuccessStatusCode().Content.ReadAsString();
        }

        public void Dispose()
        {
            Utils.DeleteDirectory(PathHelper.TestsRootPath);
            Utils.DeleteDirectory(PathHelper.SitesPath);
        }

        private HttpResponseMessage GetResponse(string url)
        {
            var client = new HttpClient();
            return client.Get(url);
        }
    }
}
