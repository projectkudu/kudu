using System;
using System.Collections.Generic;
using System.Net;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.GitServer;
using Moq;
using Xunit;
using GitServerRequestType = Kudu.Services.GitServer.CustomGitRepositoryHandler.GitServerRequestType;

namespace Kudu.Services.Test
{
    public class CustomGitRepositoryHandlerFacts
    {

        [Theory]
        // all requests scenarios with empty path (boundary)
        [InlineData("git/info/refs?service=git-upload-pack", "", GitServerRequestType.AdvertiseUploadPack)]
        [InlineData("git/info/refs?service=git-receive-pack", "", GitServerRequestType.AdvertiseReceivePack)]
        [InlineData("git/info/refs", "", GitServerRequestType.LegacyInfoRef)]
        [InlineData("git/git-receive-pack", "", GitServerRequestType.ReceivePack)]
        [InlineData("git/git-upload-pack", "", GitServerRequestType.UploadPack)]
        // all requests scenarios with non-empty path
        [InlineData("git/a/info/refs?service=git-upload-pack", @"a", GitServerRequestType.AdvertiseUploadPack)]
        [InlineData("git/a/b/info/refs?service=git-receive-pack", @"a\b", GitServerRequestType.AdvertiseReceivePack)]
        [InlineData("git/a/b/c%20d/info/refs", @"a\b\c d", GitServerRequestType.LegacyInfoRef)]
        [InlineData("git/c%3A/a/b/git-receive-pack", @"c:\a\b", GitServerRequestType.ReceivePack)]
        [InlineData("git/c%3A%5Ca%5Cb%5Cc%20d/git-upload-pack", @"c:\a\b\c d", GitServerRequestType.UploadPack)]
        // all requests scenarios with input variations that should be accept preserved
        [InlineData("Git/A//info/refs?service=git-upload-Pack&ignored", @"A", GitServerRequestType.AdvertiseUploadPack)]
        [InlineData("Git/a/B///info/refs?Service=git-receive-pack&ignored", @"a\B", GitServerRequestType.AdvertiseReceivePack)]
        [InlineData("Git///a/b/C%20d/Info///Refs", @"a\b\C d", GitServerRequestType.LegacyInfoRef)]
        [InlineData("Git/C%3A/a/b/Git-receive-pack?ignored&ignored", @"C:\a\b", GitServerRequestType.ReceivePack)]
        // This test case workd from VS but fails from build.cmd for some reason
        //[InlineData("Git/c%3A%5Ca%5C%5Cb%5Cc%20D//Git-upload-pack?ignored&ignored", @"c:\a\\b\c D", GitServerRequestType.UploadPack)]
        public void CustomGitRepositoryHandlerParseValidRequestUri(
            string requestPath,
            string expectedLocaRellPath,
            GitServerRequestType expectedRequestType)
        {
            var handler = CreateHandler();
            string localRelPath = "";
            var uri = new Uri("http://igored/" + requestPath, UriKind.Absolute);
            GitServerRequestType requestType = GitServerRequestType.Unknown;
            var ret = CustomGitRepositoryHandler.TryParseUri(uri, out localRelPath, out requestType);
            Assert.True(ret);
            Assert.Equal(expectedRequestType, requestType);
            Assert.Equal(expectedLocaRellPath, localRelPath);
        }

        [Theory]
        [InlineData("")]
        [InlineData("notgit/info/refs")]
        [InlineData("git")]
        [InlineData("git/////////////////////////")]
        [InlineData("git/bogus")]
        [InlineData("git/x%5CGit-upload-pack")]
        [InlineData("git/info/refs/?service=git-upload-pack")]
        // Legacy protocol will not add a query string, current smart protocol must contain at least ?service=
        // and ignores extra query prams to be future proof, but an info ref with an random query  we treat as invalid.
        [InlineData("git/info/refs?invalid")]
        public void CustomGitRepositoryHandlerParseInvalidRequestUri(string requestPath)
        {
            var localRelPath = "";
            var uri = new Uri("http://igored" + requestPath, UriKind.Absolute);
            var requestType = GitServerRequestType.Unknown;

            var ret = CustomGitRepositoryHandler.TryParseUri(uri, out localRelPath, out requestType);
            Assert.False(ret);
            Assert.Equal(GitServerRequestType.Unknown, requestType);
            Assert.Null(localRelPath);
        }

        static public IEnumerable<object[]> RequestScenarios
        {
            get
            {
                yield return new object[] {
                    new ValidRequest() {
                        SiteRoot = @"c:\mocksite",
                        RequestPath = "a/b",
                        RequestType = GitServerRequestType.AdvertiseUploadPack,
                        ExpectedPath = @"c:\mocksite\a\b",
                    }
                };
                yield return new object[] {
                    new ValidRequest() {
                        SiteRoot = @"c:\mocksite",
                        RequestPath = "a/b",
                        RequestType = GitServerRequestType.UploadPack,
                        ExpectedPath = @"c:\mocksite\a\b",
                    }
                };
                yield return new object[] {
                    new ValidRequest() {
                        SiteRoot = @"c:\mocksite",
                        RequestPath = "d%3A%5Cfoo",
                        RequestType = GitServerRequestType.AdvertiseUploadPack,
                        ExpectedPath = @"d:\foo",
                    }
                };
                yield return new object[] {
                    new ValidRequest() {
                        SiteRoot = @"c:\mocksite",
                        RequestPath = "d%3A%5Cfoo",
                        RequestType = GitServerRequestType.UploadPack,
                        ExpectedPath = @"d:\foo",
                    }
                };
                yield return new object[] {
                    new InvalidRequest() {
                        RequestUri = new Uri("http://ignored/"),
                        ExpectedStatus = HttpStatusCode.BadRequest,
                    }
                };
                yield return new object[] {
                    new InvalidRequest() {
                        RequestUri = new Uri("http://ignored/git/git-upload-pack"),
                        ExpectedStatus = HttpStatusCode.NotFound,
                    }
                };
                yield return new object[] {
                    new InvalidRequest() {
                        RequestUri = new Uri("http://ignored/git/info/refs?service=git-upload-pack"),
                        ExpectedStatus = HttpStatusCode.NotFound,
                    }
                };
                yield return new object[] {
                    new InvalidRequest() {
                        RequestUri = new Uri("http://ignored/git/info/refs?service=git-receive-pack"),
                        ExpectedStatus = HttpStatusCode.NotImplemented,
                    }
                };
                yield return new object[] {
                    new InvalidRequest() {
                        RequestUri = new Uri("http://ignored/git/info/refs"),
                        ExpectedStatus = HttpStatusCode.NotImplemented,
                    }
                };
                yield return new object[] {
                    new InvalidRequest() {
                        RequestUri = new Uri("http://ignored/git/git-receive-pack"),
                        ExpectedStatus = HttpStatusCode.NotImplemented,
                    }
                };
            }
        }

        [Theory]
        [MemberData("RequestScenarios")]
        public void CustomGitRepositoryHandlerBasic(IScenario scenario)
        {
            var headers = new System.Collections.Specialized.NameValueCollection();
            var request = new Mock<HttpRequestBase>();
            var response = new Mock<HttpResponseBase>();
            var context = new Mock<HttpContextBase>();
            var environment = new Mock<IEnvironment>();
            var repositoryFactory = new Mock<IRepositoryFactory>();
            var repository = new Mock<IRepository>();
            repository.SetupGet(c => c.RepositoryType).Returns(RepositoryType.Git);
            repositoryFactory.Setup(c => c.GetCustomRepository())
                .Returns(scenario.RepositoryExists ? repository.Object : null);
            environment.SetupGet(c => c.RootPath).Returns(scenario.SiteRoot);
            environment.SetupProperty(c => c.RepositoryPath);
            request.SetupGet(c => c.Url).Returns(scenario.RequestUri);
            request.SetupGet(c => c.Headers).Returns(headers);
            response.SetupGet(c => c.OutputStream).Returns(new System.IO.MemoryStream());
            response.SetupProperty(c => c.StatusCode);
            context.SetupGet(c => c.Response).Returns(response.Object);
            context.SetupGet(c => c.Request).Returns(request.Object);

            CustomGitRepositoryHandler handler =
                CreateHandler(environment: environment.Object,
                              repositoryFactory: repositoryFactory.Object);

            handler.ProcessRequestBase(context.Object);
            scenario.Verify(environment.Object, (HttpStatusCode)response.Object.StatusCode);
        }

        public interface IScenario
        {
            string SiteRoot { get; }
            bool RepositoryExists { get; }
            Uri RequestUri { get; }
            void Verify(IEnvironment env, HttpStatusCode code);
        }

        public class ValidRequest : IScenario
        {
            public string ExpectedPath { get; set; }
            public string RequestPath { get; set; }
            public string SiteRoot { get; set; }
            public GitServerRequestType RequestType { get; set; }

            public bool RepositoryExists { get { return true; } }
            public Uri RequestUri
            {
                get
                {
                    var suffix = "";
                    switch (RequestType)
                    {
                        case GitServerRequestType.UploadPack:
                            suffix = "git-upload-pack";
                            break;
                        case GitServerRequestType.AdvertiseUploadPack:
                            suffix = "info/refs?service=git-upload-pack";
                            break;
                    }
                    string format =
                        String.Format("http://ignored/git/{0}/{1}", RequestPath, suffix);
                    return new Uri(format);
                }
            }

            public void Verify(IEnvironment env, HttpStatusCode code)
            {
                Assert.Equal(HttpStatusCode.OK, code);
                Assert.Equal(ExpectedPath, env.RepositoryPath);
            }
        }

        public class InvalidRequest : IScenario
        {
            public HttpStatusCode ExpectedStatus { get; set; }
            public bool RepositoryExists { get { return false; } }
            public Uri RequestUri { get; set; }

            public void Verify(IEnvironment env, HttpStatusCode code)
            {
                Assert.Equal(ExpectedStatus, code);
            }

            public string SiteRoot
            {
                get { return @"c:\ignored"; }
            }
        }

        private CustomGitRepositoryHandler CreateHandler(
                                                   IGitServer gitServer = null,
                                                   UploadPackHandler uploadPackHandler = null,
                                                   IRepositoryFactory repositoryFactory = null,
                                                   IEnvironment environment = null)
        {

            return new CustomGitRepositoryHandler(t =>
            {
                if (t == typeof(ITracer))
                {
                    return Mock.Of<ITracer>();
                }
                else if (t == typeof(IGitServer))
                {
                    return gitServer ?? Mock.Of<IGitServer>();
                }
                else if (t == typeof(UploadPackHandler))
                {
                    return new UploadPackHandler(
                        Mock.Of<ITracer>(),
                        Mock.Of<IGitServer>(),
                        Mock.Of<IOperationLock>(),
                        Mock.Of<IDeploymentManager>());
                }
                else if (t == typeof(IRepositoryFactory))
                {
                    return repositoryFactory ?? Mock.Of<IRepositoryFactory>();
                }
                else if (t == typeof(IEnvironment))
                {
                    return environment ?? Mock.Of<IEnvironment>();
                }
                throw new NotImplementedException("type " + t.Name + " is not implemented!");
            });
        }
    }
}