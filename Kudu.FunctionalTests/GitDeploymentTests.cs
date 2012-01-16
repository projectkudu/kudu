using System;
using System.Linq;
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
            string originRepo = Git.CreateLocalRepository(repositoryName);

            using (var appManager = ApplicationManager.CreateApplication(appName))
            {
                // Act
                appManager.GitDeploy(repositoryName);
                string response = GetResponseBody(appManager.SiteUrl);
                var results = appManager.DeploymentManager.GetResults().ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Complete, results[0].Status);
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
                Assert.Equal(DeployStatus.Complete, results[0].Status);
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
                Assert.Equal(DeployStatus.Complete, results[0].Status);
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
                Assert.Equal(DeployStatus.Complete, results[0].Status);
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
                Assert.Equal(DeployStatus.Complete, results[0].Status);
                Assert.True(Utils.DirectoriesEqual(originRepo, appManager.RepositoryPath));
            }
        }

        private string GetResponseBody(string url)
        {
            HttpResponseMessage response = null;

            var client = new HttpClient();
            response = client.Get(url);

            return response.EnsureSuccessStatusCode().Content.ReadAsString();
        }

    }
}
