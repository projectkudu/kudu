using System;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Services.Test
{
    public class GenericHandlerFacts
    {
        [Theory, ClassData(typeof(SimpleTestData))]
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
            if (is_hg != null)
            {
                payload["is_hg"] = is_hg.Value;
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
        [InlineData("https://scm2.com/repo", "scm2.com")]
        [InlineData("git@scm3.com:user/repo", "scm3.com")]
        [InlineData("hg@scm4.com:user/repo", "scm4.com")]
        public void GenericHandlerDeployerTest(string url, string expected)
        {
            // Arrange
            var handler = new GenericHandler();
            var payload = new JObject();
            payload["url"] = url;

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = handler.TryParseDeploymentInfo(request: null, payload: payload, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.NotNull(deploymentInfo);
            Assert.Equal(expected, deploymentInfo.Deployer);
        }

        class SimpleTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                // Valid payload
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                    { "is_hg", true }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                    { "is_hg", "false" }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "git@scm.com:suwatch/repo" }
                }};
                yield return new object[] { DeployAction.ProcessDeployment, new Dictionary<string, object> {
                    { "url", "hg@scm.com:suwatch/repo" }
                }};

                // Invalid payload
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                }};
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                    { "url", "http://scm.com/repo" },
                    { "garbage", 1 }
                }};
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                    { "url", "invalid_uri" }
                }};
                yield return new object[] { DeployAction.UnknownPayload, new Dictionary<string, object> {
                    { "url", 1 },
                }};
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}