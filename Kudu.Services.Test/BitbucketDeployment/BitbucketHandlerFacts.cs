using System.Web;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Services.Test
{
    public class BitbucketHandlerFacts
    {
        [Fact]
        public void BitbucketHandlerIgnoresNonBitbucketPayloads()
        {
            // Arrange
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: null, targetBranch: null, deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.UnknownPayload, result);
        }

        [Fact]
        public void BitbucketHandlerReturnsNoOpForCommitsThatAreNotTheTargetBranch()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""default"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Some more changes"", ""node"": ""0bbefd70c4c4"", ""parents"": [ ""3cb8bf8aec0a"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""revision"": 4, ""size"": -1, ""timestamp"": ""2012-12-17 19:41:28"", ""utctimestamp"": ""2012-12-17 18:41:28+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/hellomercurial/"", ""fork"": false, ""is_private"": false, ""name"": ""HelloMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""hellomercurial"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "not-default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.NoOp, result);
            Assert.Null(deploymentInfo);
        }

        [Fact]
        public void BitbucketHandlerAllowsPayloadsWithNullBranch()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", 
                    ""commits"": [ { ""author"": ""pranavkm"", ""branch"": null, ""raw_node"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""message"": ""Some file changes"" }],
                    ""repository"": { ""absolute_url"": ""/kudutest/hellomercurial/"", ""is_private"": false, ""name"": ""HelloMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "not-default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Mercurial, deploymentInfo.RepositoryType);
            Assert.Equal("https://bitbucket.org/kudutest/hellomercurial/", deploymentInfo.RepositoryUrl);
            Assert.Equal("pranavkm", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("0bbefd70c4c4213bba1e91998141f6e861cec24d", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Some file changes", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void BitbucketHandlerParsesBitbucketPayloadsForMercurialRepositories()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""default"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Some more changes"", ""node"": ""0bbefd70c4c4"", ""parents"": [ ""3cb8bf8aec0a"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""revision"": 4, ""size"": -1, ""timestamp"": ""2012-12-17 19:41:28"", ""utctimestamp"": ""2012-12-17 18:41:28+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/hellomercurial/"", ""fork"": false, ""is_private"": false, ""name"": ""HelloMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""hellomercurial"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Mercurial, deploymentInfo.RepositoryType);
            Assert.Equal("https://bitbucket.org/kudutest/hellomercurial/", deploymentInfo.RepositoryUrl);
            Assert.Equal("pranavkm", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("0bbefd70c4c4213bba1e91998141f6e861cec24d", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Some more changes", deploymentInfo.TargetChangeset.Message);
        }


        [Fact]
        public void BitbucketHandlerParsesBitbucketPayloadsForPrivateMercurialRepositories()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""default"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Some more changes"", ""node"": ""0bbefd70c4c4"", ""parents"": [ ""3cb8bf8aec0a"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""revision"": 4, ""size"": -1, ""timestamp"": ""2012-12-17 19:41:28"", ""utctimestamp"": ""2012-12-17 18:41:28+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/hellomercurial/"", ""fork"": false, ""is_private"": true, ""name"": ""HelloMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""hellomercurial"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Mercurial, deploymentInfo.RepositoryType);
            Assert.Equal("ssh://hg@bitbucket.org/kudutest/hellomercurial/", deploymentInfo.RepositoryUrl);
            Assert.Equal("pranavkm", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("0bbefd70c4c4213bba1e91998141f6e861cec24d", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Some more changes", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void BitbucketHandlerParsesBitbucketPayloadsForMercurialRepositoriesWithMultipleCommitsAcrossBranches()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""default"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Fix Hello.txt"", ""node"": ""dabec27eeec9"", ""parents"": [ ""478b0d4d794c"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""dabec27eeec9d85175a4dcbeb83b65189c929b68"", ""revision"": 7, ""size"": -1, ""timestamp"": ""2012-12-17 21:47:21"", ""utctimestamp"": ""2012-12-17 20:47:21+00:00"" }, { ""author"": ""pranavkm"", ""branch"": ""default"", ""files"": [ { ""file"": ""HelloWorld.txt"", ""type"": ""added"" } ], ""message"": ""Adding hello world"", ""node"": ""42c0d799763d"", ""parents"": [ ""dabec27eeec9"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""42c0d799763d7acbe4312d000f771ec0afa0d6ab"", ""revision"": 8, ""size"": -1, ""timestamp"": ""2012-12-17 21:47:46"", ""utctimestamp"": ""2012-12-17 20:47:46+00:00"" }, { ""author"": ""pranavkm"", ""branch"": ""Test-Branch"", ""files"": [ { ""file"": ""HelloWorld.txt"", ""type"": ""modified"" } ], ""message"": ""Fixing hello world"", ""node"": ""16ea3237dbcd"", ""parents"": [ ""42c0d799763d"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""16ea3237dbcd492b170c28ae0060791a1c170c0c"", ""revision"": 9, ""size"": -1, ""timestamp"": ""2012-12-17 21:48:13"", ""utctimestamp"": ""2012-12-17 20:48:13+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/hellomercurial/"", ""fork"": false, ""is_private"": false, ""name"": ""HelloMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""hellomercurial"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "default", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Mercurial, deploymentInfo.RepositoryType);
            Assert.Equal("https://bitbucket.org/kudutest/hellomercurial/", deploymentInfo.RepositoryUrl);
            Assert.Equal("pranavkm", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("42c0d799763d7acbe4312d000f771ec0afa0d6ab", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Adding hello world", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void BitbucketHandlerParsesBitbucketPayloadsForGitRepositories()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""Pranav K"", ""branch"": ""master"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Some changes to Hello.txt\n"", ""node"": ""0b418c5fd473"", ""parents"": [ ""ec8a72695042"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""0b418c5fd473474f197071ec75cd664937d6565d"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 22:01:22"", ""utctimestamp"": ""2012-12-17 21:01:22+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/mypublicrepo/"", ""fork"": false, ""is_private"": false, ""name"": ""MyPublicRepo"", ""owner"": ""kudutest"", ""scm"": ""git"", ""slug"": ""mypublicrepo"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("https://bitbucket.org/kudutest/mypublicrepo/", deploymentInfo.RepositoryUrl);
            Assert.Equal("Pranav K", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("0b418c5fd473474f197071ec75cd664937d6565d", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Some changes to Hello.txt", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void BitbucketHandlerParsesBitbucketPayloadsForGitRepositoriesWithMultipleCommits()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""Pranav K"", ""branch"": null, ""branches"": [], ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Changes 1\n"", ""node"": ""176a1c27dde2"", ""parents"": [ ""0b418c5fd473"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""176a1c27dde2d397a69dd859cb6df8087403a07a"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 23:31:14"", ""utctimestamp"": ""2012-12-17 22:31:14+00:00"" }, { ""author"": ""Pranav K"", ""branch"": ""foo"", ""files"": [ { ""file"": ""Foo.txt"", ""type"": ""added"" } ], ""message"": ""Foo commit\n"", ""node"": ""f94996d67d6d"", ""parents"": [ ""e689ee0adcb0"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""f94996d67d6d5a060aaf2fcb72c333d0899549ab"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 23:32:20"", ""utctimestamp"": ""2012-12-17 22:32:20+00:00"" }, { ""author"": ""Pranav K"", ""branch"": ""master"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Some changes to Hello\n"", ""node"": ""d3bde12dfe11"", ""parents"": [ ""176a1c27dde2"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""d3bde12dfe11206173fa940b3e50b135e9ae1677"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 23:32:44"", ""utctimestamp"": ""2012-12-17 22:32:44+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/mypublicrepo/"", ""fork"": false, ""is_private"": false, ""name"": ""MyPublicRepo"", ""owner"": ""kudutest"", ""scm"": ""git"", ""slug"": ""mypublicrepo"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("https://bitbucket.org/kudutest/mypublicrepo/", deploymentInfo.RepositoryUrl);
            Assert.Equal("Pranav K", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("d3bde12dfe11206173fa940b3e50b135e9ae1677", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Some changes to Hello", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void BitbucketHandlerParsesBitbucketPayloadsForPrivateGitRepositories()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""Pranav K"", ""branch"": null, ""branches"": [], ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Changes 1\n"", ""node"": ""176a1c27dde2"", ""parents"": [ ""0b418c5fd473"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""176a1c27dde2d397a69dd859cb6df8087403a07a"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 23:31:14"", ""utctimestamp"": ""2012-12-17 22:31:14+00:00"" }, { ""author"": ""Pranav K"", ""branch"": ""foo"", ""files"": [ { ""file"": ""Foo.txt"", ""type"": ""added"" } ], ""message"": ""Foo commit\n"", ""node"": ""f94996d67d6d"", ""parents"": [ ""e689ee0adcb0"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""f94996d67d6d5a060aaf2fcb72c333d0899549ab"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 23:32:20"", ""utctimestamp"": ""2012-12-17 22:32:20+00:00"" }, { ""author"": ""Pranav K"", ""branch"": ""master"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Some changes to Hello\n"", ""node"": ""d3bde12dfe11"", ""parents"": [ ""176a1c27dde2"" ], ""raw_author"": ""Pranav K <prkrishn@hotmail.com>"", ""raw_node"": ""d3bde12dfe11206173fa940b3e50b135e9ae1677"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-12-17 23:32:44"", ""utctimestamp"": ""2012-12-17 22:32:44+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/myprivaterepo.git"", ""fork"": false, ""is_private"": true, ""name"": ""MyPrivateRepo"", ""owner"": ""kudutest"", ""scm"": ""git"", ""slug"": ""myprivaterepo"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("ssh://git@bitbucket.org/kudutest/myprivaterepo.git", deploymentInfo.RepositoryUrl);
            Assert.Equal("Pranav K", deploymentInfo.TargetChangeset.AuthorName);
            Assert.Equal("d3bde12dfe11206173fa940b3e50b135e9ae1677", deploymentInfo.TargetChangeset.Id);
            Assert.Equal("Some changes to Hello", deploymentInfo.TargetChangeset.Message);
        }

        [Fact]
        public void BitbucketDoesNotReturnNoOpForDeleteOperations()
        {
            // Arrange
            string payloadContent = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [], ""repository"": { ""absolute_url"": ""/kudutest/myprivaterepo/"", ""fork"": false, ""is_private"": false, ""name"": ""MyprivateRepo"", ""owner"": ""kudutest"", ""scm"": ""git"", ""slug"": ""myprivaterepo"", ""website"": """" }, ""user"": ""kudutest"" }";

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.UserAgent).Returns("Bitbucket.org");
            var bitbucketHandler = new BitbucketHandler();

            // Act
            DeploymentInfo deploymentInfo;
            DeployAction result = bitbucketHandler.TryParseDeploymentInfo(httpRequest.Object, payload: JObject.Parse(payloadContent), targetBranch: "master", deploymentInfo: out deploymentInfo);

            // Assert
            Assert.Equal(DeployAction.ProcessDeployment, result);
            Assert.Equal("Bitbucket", deploymentInfo.Deployer);
            Assert.Equal(RepositoryType.Git, deploymentInfo.RepositoryType);
            Assert.Equal("https://bitbucket.org/kudutest/myprivaterepo/", deploymentInfo.RepositoryUrl);
            Assert.Empty(deploymentInfo.TargetChangeset.Id);
            Assert.Null(deploymentInfo.TargetChangeset.AuthorName);
            Assert.Null(deploymentInfo.TargetChangeset.Message);
        }
    }
}
