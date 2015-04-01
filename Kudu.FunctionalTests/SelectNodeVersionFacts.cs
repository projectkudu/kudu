using System.Linq;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class SelectNodeVersionFacts
    {
        [Fact]
        public void TestFallbackToDefaultNodeJsVersionWithServerJsOnly()
        {
            // Arrange
            string appName = "FallbackToDefaultNodeJsVersionWithServerJsOnly";

            using (var repo = Git.Clone("VersionPinnedNodeJsApp"))
            {
                repo.DeleteFile("iisnode.yml");
                repo.DeleteFile("package.json");

                Git.Commit(repo.PhysicalPath, "Changes");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Contains("The package.json file does not specify node.js engine version constraints", deployResult.GitTrace);
                });
            }
        }

        [Fact]
        public void TestFallbackToDefaultNodeJsVersionWithServerJsAndEmptyPackageJson()
        {
            // Arrange
            string appName = "FallbackToDefaultNodeJsVersionWithServerJsAndEmptyPackageJson";

            using (var repo = Git.Clone("VersionPinnedNodeJsApp"))
            {
                repo.DeleteFile("iisnode.yml");
                repo.WriteFile("package.json", @"{ ""name"" : ""foo"", ""version"": ""1.0.0"" }");

                Git.Commit(repo.PhysicalPath, "Changes");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Contains("The package.json file does not specify node.js engine version constraints", deployResult.GitTrace);
                });
            }

        }

        [Fact]
        public void TestFallbackToDefaultNodeJsVersionWithIisnodeYmlLackingNodeProcessCommandLine()
        {
            // Arrange
            string appName = "FallbackToDefaultNodeJsVersionWithIisnodeYmlLacking";

            using (var repo = Git.Clone("VersionPinnedNodeJsApp"))
            {
                repo.WriteFile("iisnode.yml", "foo: bar");
                repo.DeleteFile("package.json");
                Git.Commit(repo.PhysicalPath, "Changes");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Contains("The package.json file does not specify node.js engine version constraints", deployResult.GitTrace);
                });
            }
        }

        [Fact]
        public void TestTurningAutomaticVersionSelectionOffWithIisnodeYmlWithNodeProcessCommandLine()
        {
            // Arrange
            string appName = "TurningAutomaticVersionSelectionOffWithIisnodeYml";

            using (var repo = Git.Clone("VersionPinnedNodeJsApp"))
            {
                repo.WriteFile("iisnode.yml", "nodeProcessCommandLine: bar");
                Git.Commit(repo.PhysicalPath, "Changes");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Contains("The iisnode.yml file explicitly sets nodeProcessCommandLine", deployResult.GitTrace);
                });
            }
        }

        [Fact]
        public void TestMismatchBetweenAvailableVersionsAndRequestedVersions()
        {
            // Arrange
            string appName = "TestMismatchBetweenAvailableVersionsAndRequestedVersions";

            using (var repo = Git.Clone("VersionPinnedNodeJsApp"))
            {
                repo.Replace("package.json", "0.8.2", "0.1.0");
                Git.Commit(repo.PhysicalPath, "Changes");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Contains("No available node.js version matches application's version constraint of '0.1.0'", deployResult.GitTrace);
                });
            }
        }

        [Fact]
        public void TestPositiveMatch()
        {
            // Arrange
            string appName = "VersionPinnedNodeJsApp";

            using (var repo = Git.Clone("VersionPinnedNodeJsApp"))
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
                });
            }
        }

        [Fact]
        public void TestNodeCustomStartScriptFromPackageJson()
        {
            // Arrange
            string appName = "TestCustomStartScriptFromPackageJson";

            using (var repo = Git.Clone("NodeWithCustomStartScript"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert deployment success
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    // Verify log output
                    Assert.Contains("Using start-up script app/app.js", deployResult.GitTrace);
                    Assert.Contains("Updating iisnode.yml", deployResult.GitTrace);
                    // Verify app works. 
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Main App!");
                    // Verify that node-inspector works
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "/app/app.js/debug", "inspector");
                });
            }
        }
    }
}
