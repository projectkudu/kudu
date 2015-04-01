using System;
using System.Collections.Generic;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class GenericHandlerFacts
    {
        [Theory, MemberData("SimpleTestData")]
        public void GenericHandlerSimpleTest(DeployAction expected, IDictionary<string, object> values)
        {
            // Arrange
            var handler = new GenericHandler();
            var payload = new JObject();
            foreach (var pair in values)
            {
                payload[pair.Key] = JToken.FromObject(pair.Value);
            }

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(request: null, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("https://scm.com/repo", null, RepositoryType.Git)]
        [InlineData("https://scm.com/repo", false, RepositoryType.Git)]
        [InlineData("https://scm.com/repo", true, RepositoryType.Mercurial)]
        [InlineData("git@scm.com:user/repo", null, RepositoryType.Git)]
        [InlineData("git@scm.com:user/repo", false, RepositoryType.Git)]
        [InlineData("git@scm.com:user/repo", true, RepositoryType.Mercurial)]
        [InlineData("hg@scm.com:user/repo", null, RepositoryType.Mercurial)]
        [InlineData("hg@scm.com:user/repo", false, RepositoryType.Git)]
        [InlineData("hg@scm.com:user/repo", true, RepositoryType.Mercurial)]
        public void GenericHandlerRepositoryTypeTest(string url, bool? is_hg, RepositoryType expected)
        {
            // Arrange
            var handler = new GenericHandler();
            var payload = new JObject();
            payload["url"] = url;
            payload["format"] = "basic";
            if (is_hg != null)
            {
                payload["scm"] = is_hg.Value ? "hg" : "git";
            }

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(request: null, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal(expected, deploymentInfo.RepositoryType);
        }

        [Theory]
        [InlineData("http://scm1.com/repo", "scm1.com")]
        [InlineData("git@scm2.com:user/repo", "scm2.com")]
        [InlineData("https://www.github.com/repo", "GitHub")]
        [InlineData("hg@scm.Bitbucket.org:user/repo", "Bitbucket")]
        [InlineData("https://www.codeplex.com/repo", "CodePlex")]
        [InlineData("http://gitlab.proscat.nl/inspectbin", "GitlabHQ")]
        [InlineData("https://www.kilnhg.com/repo", "Kiln")]
        public void GenericHandlerDeployerTest(string url, string expected)
        {
            // Arrange
            var handler = new GenericHandler();
            var payload = new JObject();
            payload["url"] = url;
            payload["format"] = "basic";

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(request: null, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal(expected, deploymentInfo.Deployer);
        }

        [Theory]
        [InlineData("http://scm1.com/repo", "http://scm1.com/repo", null)]
        [InlineData("http://scm1.com/repo#", "http://scm1.com/repo", null)]
        [InlineData("http://scm1.com/repo#1234567", "http://scm1.com/repo", "1234567")]
        [InlineData("http://###:###@scm1.com/repo# 678 ", "http://###:###@scm1.com/repo", " 678 ")]
        [InlineData("#", "", null)]
        [InlineData("## ", "#", " ")]
        public void GenericHandlerBranchTest(string url, string repoUrl, string commitId)
        {
            // Act
            var deploymentInfo = new DeploymentInfo();

            GenericHandler.SetRepositoryUrl(deploymentInfo, url);
            
            // Assert
            Assert.Equal(repoUrl, deploymentInfo.RepositoryUrl);
            Assert.Equal(commitId, deploymentInfo.CommitId);
        }

        [Theory]
        [InlineData("invalid_url")]
        [InlineData("git@scm.com")]
        [InlineData("scm.com:user/repo")]
        [InlineData("git:scm.com@user/repo")]
        public void GenericHandlerInvalidUrl(string url)
        {
            // Arrange
            var handler = new GenericHandler();
            var payload = new JObject();
            payload["url"] = url;
            payload["format"] = "basic";

            // Act
            DeploymentInfo deploymentInfo;

            // Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                handler.TryParseDeploymentInfo(request: null, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);
            });
        }

        public static IEnumerable<object[]> SimpleTestData
        {
            get
            {
                // Valid payload
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                    { "format", "basic" }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                    { "format", "basic" },
                    { "is_hg", true }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                    { "format", "basic" },
                    { "is_hg", "false" }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "git@scm.com:suwatch/repo" },
                    { "format", "basic" }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "hg@scm.com:suwatch/repo" },
                    { "format", "basic" },
                    { "garbage", 1 }
                }};

                // Invalid payload
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                }};
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                }};
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                    { "format", "basic" }
                }};
            }
        }
    }
}