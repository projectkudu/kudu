using System;
using System.Web;
using Kudu.Core.Test;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class KilnHgHandlerFacts
    {
        [Fact]
        public void KilnHgHandlerIgnoresNonKilnPayloads()
        {
            // Arrange
            var payload = JObject.Parse(@"{ ""repository"":{ ""repourl"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1/commit/840daf31f4f87cb5cafd295ef75de989095f415b"" } }");
            var httpRequest = new Mock<HttpRequestBase>();
            var settingsManager = new MockDeploymentSettingsManager();
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = kilnHandler.TryParseDeploymentInfo(httpRequest.Object, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Fact]
        public void KilnHgHandlerReturnsNoOpForCommitsThatAreNotTheTargetBranch()
        {
            // Arrange
            const string payload = @" { ""commits"": [ { ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""771363bfb8e6e2b76a3da8d156c6a3db0ea9a9c4"", ""message"": ""did a commit"", ""revision"": 1, ""timestamp"": ""1/7/2013 6:54:25 AM"" } ], ""repository"": { ""url"": ""https://kudutest.kilnhg.com/Code/Test/Group/KuduApp"" } } ";
            var httpRequest = new Mock<HttpRequestBase>();
            var settingsManager = new MockDeploymentSettingsManager();
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = kilnHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payload), targetBranch: "not-default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
            Assert.Null(deploymentInfo);
        }

        [Theory]
        [InlineData("http://kudutest.kilnhg.com/Code/Test/Group/KuduApp")]
        [InlineData("https://kudutest.kilnhg.com/Code/Test/Group/KuduApp")]
        public void KilnHgHandlerParsesKilnPayloads(string repositoryUrl)
        {
            // Arrange
            string payload = @"{ ""commits"": [ { ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""f1525c29206072f6565e6ba70831afb65b55e9a0"", ""message"": ""commit message"", ""revision"": 14, ""timestamp"": ""1/15/2013 2:23:37 AM"" } ], ""repository"": { ""url"": """ + repositoryUrl + @""" } }";
            var httpRequest = new Mock<HttpRequestBase>();
            var settingsManager = new MockDeploymentSettingsManager();
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = kilnHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payload), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Kiln", deploymentInfo.Deployer);
            Assert.Equal(repositoryUrl, deploymentInfo.RepositoryUrl);
            Assert.Equal("Brian Surowiec", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("xtorted@optonline.net", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("f1525c29206072f6565e6ba70831afb65b55e9a0", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("commit message", deploymentInfo.TargetChangeset.Message);
            Assert.Equal(new DateTimeOffset(2013, 1, 15, 2, 23, 37, TimeSpan.Zero), deploymentInfo.TargetChangeset.Timestamp);
        }

        [Fact]
        public void KilnHgHandlerParsesKilnPayloadsWithAccessTokenCommit()
        {
            // Arrange
            const string payload = @"{ ""commits"": [ { ""author"": ""a1444778-8d5d-413d-83f7-6dbf9e2cd77d"", ""branch"": ""default"", ""id"": ""f1525c29206072f6565e6ba70831afb65b55e9a0"", ""message"": ""commit message"", ""revision"": 14, ""timestamp"": ""1/15/2013 2:23:37 AM"" } ], ""repository"": { ""url"": ""https://kudutest.kilnhg.com/Code/Test/Group/KuduApp"" } }";
            var httpRequest = new Mock<HttpRequestBase>();
            var settingsManager = new MockDeploymentSettingsManager();
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = kilnHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payload), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Kiln", deploymentInfo.Deployer);
            Assert.Equal("https://kudutest.kilnhg.com/Code/Test/Group/KuduApp", deploymentInfo.RepositoryUrl);
            Assert.Equal("System Account", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Null(deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("f1525c29206072f6565e6ba70831afb65b55e9a0", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("commit message", deploymentInfo.TargetChangeset.Message);
            Assert.Equal(new DateTimeOffset(2013, 1, 15, 2, 23, 37, TimeSpan.Zero), deploymentInfo.TargetChangeset.Timestamp);
        }

        [Theory]
        [InlineData("http://kudutest.kilnhg.com/Code/Test/Group/KuduApp")]
        [InlineData("https://kudutest.kilnhg.com/Code/Test/Group/KuduApp")]
        public void KilnHgHandlerParsesKilnPayloadsForPrivateRepositories(string repositoryUrl)
        {
            // Arrange
            string payload = @"{ ""commits"": [ { ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""771363bfb8e6e2b76a3da8d156c6a3db0ea9a9c4"", ""message"": ""commit message"", ""revision"": 1, ""timestamp"": ""1/7/2013 6:54:25 AM"" } ], ""repository"": { ""url"": """ + repositoryUrl + @""" } } ";
            var httpRequest = new Mock<HttpRequestBase>();
            var settingsManager = new MockDeploymentSettingsManager();
            settingsManager.SetValue("kiln.accesstoken", "hg-user");
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = kilnHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payload), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Kiln", deploymentInfo.Deployer);
            Assert.Equal("https://hg-user:kudu@kudutest.kilnhg.com/Code/Test/Group/KuduApp", deploymentInfo.RepositoryUrl);
            Assert.Equal("Brian Surowiec", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("xtorted@optonline.net", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("771363bfb8e6e2b76a3da8d156c6a3db0ea9a9c4", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("commit message", deploymentInfo.TargetChangeset.Message);
            Assert.Equal(new DateTimeOffset(2013, 1, 7, 6, 54, 25, TimeSpan.Zero), deploymentInfo.TargetChangeset.Timestamp);
        }

        [Fact]
        public void KilnHgHandlerParsesKilnPayloadsForRepositoriesWithMultipleCommitsAccrossBranches()
        {
            // Arrange
            const string payload = @"{ ""commits"": ["
                                     + @"{ ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""non-default"", ""id"": ""f1525c29206072f6565e6ba70831afb65b55e9a0"", ""message"": ""commit message 14"", ""revision"": 14, ""timestamp"": ""1/15/2013 2:23:37 AM"" },"
                                     + @"{ ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""58df029b9891bed6be1516971b50dc0eda58ce38"", ""message"": ""commit message 13"", ""revision"": 13, ""timestamp"": ""1/15/2013 2:23:20 AM"" },"
                                     + @"{ ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""cb6ea738f5ec16d53c06a2f5823c34b396922c13"", ""message"": ""commit message 12"", ""revision"": 12, ""timestamp"": ""1/15/2013 2:23:04 AM"" }"
                                 + @"], ""repository"": { ""url"": ""https://kudutest.kilnhg.com/Code/Test/Group/KuduApp"" } }";
            var httpRequest = new Mock<HttpRequestBase>();
            var settingsManager = new MockDeploymentSettingsManager();
            settingsManager.SetValue("kiln.accesstoken", "hg-user");
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = kilnHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payload), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Kiln", deploymentInfo.Deployer);
            Assert.Equal("https://hg-user:kudu@kudutest.kilnhg.com/Code/Test/Group/KuduApp", deploymentInfo.RepositoryUrl);
            Assert.Equal("Brian Surowiec", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("xtorted@optonline.net", deploymentInfo.TargetChangeset.AuthorEmail);
            Assert.Equal("58df029b9891bed6be1516971b50dc0eda58ce38", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("commit message 13", deploymentInfo.TargetChangeset.Message);
            Assert.Equal(new DateTimeOffset(2013, 1, 15, 2, 23, 20, TimeSpan.Zero), deploymentInfo.TargetChangeset.Timestamp);
        }

        [Theory]
        [InlineData(@"{ ""repo"": { ""repo_url"": ""https://kudutest.kilnhg.com/Code/Test/Group/KuduApp"" } } ")]
        [InlineData(@"{ ""repository"": { ""repo_url"": ""https://kudutest.kilnhg.com/Code/Test/Group/KuduApp"" } } ")]
        public void IsKilnRequestWithoutRepositoryUrl(string payloadContent)
        {
            // Arrange
            var settingsManager = new MockDeploymentSettingsManager();
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            bool result = kilnHandler.IsKilnRequest(JObject.Parse(payloadContent));

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(true, @"\.kilnhg\.com")]
        [InlineData(false, @"\.github\.com")]
        public void IsKilnRequestWithCustomDomainPatterns(bool expectedResult, string domainPattern)
        {
            // Arrange
            const string payload = @"{ ""repository"": { ""url"": ""https://kudu.kilnhg.com/Code/Test/Group/KuduApp"" } } ";

            var settingsManager = new MockDeploymentSettingsManager();
            settingsManager.SetValue("kiln.domain", domainPattern);

            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            bool result = kilnHandler.IsKilnRequest(JObject.Parse(payload));

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(true, @"kilnhg.com")]
        [InlineData(false, @"github.com")]
        public void IsKilnRequestWithDefaultDomainPatterns(bool expectedResult, string domain)
        {
            // Arrange
            var payload = string.Format(@"{{ ""repository"": {{ ""url"": ""https://kudu.{0}/Code/Test/Group/KuduApp"" }} }} ", domain);
            var settingsManager = new MockDeploymentSettingsManager();
            var kilnHandler = new KilnHgHandler(settingsManager);

            // Act
            bool result = kilnHandler.IsKilnRequest(JObject.Parse(payload));

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("Test User <test@user.com>")] // full, correct, format
        [InlineData("  <test@user.com>")] // missing first & last name, with spaces
        [InlineData("Test <test@user.com>")] // only first name
        [InlineData(" User <test@user.com>")] // only last name, with spaces
        [InlineData("<test@user.com>")] // only email, no spaces
        [InlineData("test@user.com")] // only email, no other formatting
        public void ParseEmailFromAuthorWithEmailAddress(string author)
        {
            Assert.Equal("test@user.com", KilnHgHandler.ParseEmailFromAuthor(author));
        }

        [Theory]
        [InlineData("Test User")]
        [InlineData("Test User <>")]
        [InlineData("Test User <email>")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("\t")]
        [InlineData("    ")]
        public void ParseEmailFromAuthorWithoutEmailAddress(string author)
        {
            Assert.Null(KilnHgHandler.ParseEmailFromAuthor(author));
        }

        [Theory]
        [InlineData("Test User", "Test User <test@user.com>")] // full, correct, format
        [InlineData("<test@user.com>", "  <test@user.com>")] // missing first & last name, with spaces
        [InlineData("Test", "Test <test@user.com>")] // only first name
        [InlineData("User", " User <test@user.com>")] // only last name, with spaces
        [InlineData("<test@user.com>", "<test@user.com>")] // only email, no spaces
        [InlineData("test@user.com", "test@user.com")] // only email, no other formatting
        [InlineData("Test User", " Test User ")]
        [InlineData("Test User", "Test User <>")]
        [InlineData("Test User", "Test User <email>")]
        [InlineData(null, null)]
        [InlineData(null, "")]
        [InlineData(null, "\t")]
        [InlineData(null, "    ")]
        public void ParseNameFromAuthor(string expectedResult, string author)
        {
            Assert.Equal(expectedResult, KilnHgHandler.ParseNameFromAuthor(author));
        }
    }
}
