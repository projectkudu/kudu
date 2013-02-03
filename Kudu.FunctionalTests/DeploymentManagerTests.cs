using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class DeploymentManagerTests
    {
        [Fact]
        public void DeploymentApisReturn404IfDeploymentIdDoesntExist()
        {
            string appName = "Rtn404IfDeployIdNotExist";

            ApplicationManager.Run(appName, appManager =>
            {
                string id = "foo";
                var ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeleteAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeployAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeployAsync(id, clean: true).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.GetLogEntriesAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.GetResultAsync(id).Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);

                ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, "fakeId").Wait());
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);
            });
        }

        [Fact]
        public void DeploymentApis()
        {
            // Arrange

            string appName = "DeploymentApis";

            using (var repo = Git.Clone("HelloWorld"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(1, results.Count);
                    var result = results[0];
                    Assert.Equal("davidebbo", result.Author);
                    Assert.Equal("david.ebbo@microsoft.com", result.AuthorEmail);
                    Assert.True(result.Current);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.NotNull(result.Url);
                    Assert.NotNull(result.LogUrl);
                    Assert.True(String.IsNullOrEmpty(result.Deployer));

                    ICredentials cred = appManager.DeploymentManager.Credentials;
                    KuduAssert.VerifyUrl(result.Url, cred);
                    KuduAssert.VerifyUrl(result.LogUrl, cred);

                    var resultAgain = appManager.DeploymentManager.GetResultAsync(result.Id).Result;
                    Assert.Equal("davidebbo", resultAgain.Author);
                    Assert.Equal("david.ebbo@microsoft.com", resultAgain.AuthorEmail);
                    Assert.True(resultAgain.Current);
                    Assert.Equal(DeployStatus.Success, resultAgain.Status);
                    Assert.NotNull(resultAgain.Url);
                    Assert.NotNull(resultAgain.LogUrl);
                    KuduAssert.VerifyUrl(resultAgain.Url, cred);
                    KuduAssert.VerifyUrl(resultAgain.LogUrl, cred);

                    repo.WriteFile("HelloWorld.txt", "This is a test");
                    Git.Commit(repo.PhysicalPath, "Another commit");
                    appManager.GitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    string oldId = results[1].Id;

                    // Delete one
                    appManager.DeploymentManager.DeleteAsync(oldId).Wait();

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(1, results.Count);
                    Assert.NotEqual(oldId, results[0].Id);

                    result = results[0];

                    // Redeploy
                    appManager.DeploymentManager.DeployAsync(result.Id).Wait();

                    // Clean deploy
                    appManager.DeploymentManager.DeployAsync(result.Id, clean: true).Wait();

                    var entries = appManager.DeploymentManager.GetLogEntriesAsync(result.Id).Result.ToList();

                    Assert.True(entries.Count > 0);

                    // First entry is always null
                    Assert.Null(entries[0].DetailsUrl);

                    var entryWithDetails = entries.First(e => e.DetailsUrl != null);

                    var nested = appManager.DeploymentManager.GetLogEntryDetailsAsync(result.Id, entryWithDetails.Id).Result.ToList();

                    Assert.True(nested.Count > 0);

                    KuduAssert.VerifyLogOutput(appManager, result.Id, "Cleaning Git repository");

                    // Can't delete the active one
                    var ex = KuduAssert.ThrowsUnwrapped<HttpRequestException>(() => appManager.DeploymentManager.DeleteAsync(result.Id).Wait());
                    Assert.Equal("Response status code does not indicate success: 409 (Conflict).", ex.Message);
                });
            }
        }

        [Fact]
        public void DeploymentManagerExtensibility()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "DeploymentApis";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    var handler = new FakeMessageHandler()
                    {
                        InnerHandler = HttpClientHelper.CreateClientHandler(appManager.DeploymentManager.ServiceUrl, appManager.DeploymentManager.Credentials)
                    };

                    var manager = new RemoteDeploymentManager(appManager.DeploymentManager.ServiceUrl, appManager.DeploymentManager.Credentials, handler);
                    var results = manager.GetResultsAsync().Result.ToList();

                    Assert.Equal(0, results.Count);
                    Assert.NotNull(handler.Url);
                });
            }
        }

        [Fact]
        public void DeleteKuduSiteCleansEverything()
        {
            // Arrange
            string appName = "DeleteKuduSiteCleansEverything";

            using (var repo = Git.Clone("HelloWorld"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);

                    appManager.RepositoryManager.Delete().Wait();
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(0, results.Count);
                });
            }
        }

        [Fact]
        public void PullApiTestGitHubFormat()
        {
            string githubPayload = @"{ ""after"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""before"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"",  ""commits"": [ { ""added"": [], ""author"": { ""email"": ""prkrishn@hotmail.com"", ""name"": ""Pranav K"", ""username"": ""pranavkm"" }, ""id"": ""43acf30efa8339103e2bed5c6da1379614b00572"", ""message"": ""Changes from master again"", ""modified"": [ ""Hello.txt"" ], ""timestamp"": ""2012-12-17T17:32:15-08:00"" } ], ""compare"": ""https://github.com/KuduApps/GitHookTest/compare/7e2a599e2d28...7e2a599e2d28"", ""created"": false, ""deleted"": false, ""forced"": false, ""head_commit"": { ""added"": [ "".gitignore"", ""SimpleWebApplication.sln"", ""SimpleWebApplication/About.aspx"", ""SimpleWebApplication/About.aspx.cs"", ""SimpleWebApplication/About.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx"", ""SimpleWebApplication/Account/ChangePassword.aspx.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.designer.cs"", ""SimpleWebApplication/Account/Login.aspx"", ""SimpleWebApplication/Account/Login.aspx.cs"", ""SimpleWebApplication/Account/Login.aspx.designer.cs"", ""SimpleWebApplication/Account/Register.aspx"", ""SimpleWebApplication/Account/Register.aspx.cs"", ""SimpleWebApplication/Account/Register.aspx.designer.cs"", ""SimpleWebApplication/Account/Web.config"", ""SimpleWebApplication/Default.aspx"", ""SimpleWebApplication/Default.aspx.cs"", ""SimpleWebApplication/Default.aspx.designer.cs"", ""SimpleWebApplication/Global.asax"", ""SimpleWebApplication/Global.asax.cs"", ""SimpleWebApplication/Properties/AssemblyInfo.cs"", ""SimpleWebApplication/Scripts/jquery-1.4.1-vsdoc.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.min.js"", ""SimpleWebApplication/SimpleWebApplication.csproj"", ""SimpleWebApplication/Site.Master"", ""SimpleWebApplication/Site.Master.cs"", ""SimpleWebApplication/Site.Master.designer.cs"", ""SimpleWebApplication/Styles/Site.css"", ""SimpleWebApplication/Web.Debug.config"", ""SimpleWebApplication/Web.Release.config"", ""SimpleWebApplication/Web.config"" ], ""author"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""committer"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""distinct"": false, ""id"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""message"": ""Initial"", ""modified"": [], ""removed"": [], ""timestamp"": ""2011-11-21T23:07:42-08:00"", ""url"": ""https://github.com/KuduApps/GitHookTest/commit/7e2a599e2d28665047ec347ab36731c905c95e8b"" }, ""pusher"": { ""name"": ""none"" }, ""ref"": ""refs/heads/master"", ""repository"": { ""created_at"": ""2012-06-28T00:07:55-07:00"", ""description"": """", ""fork"": false, ""forks"": 1, ""has_downloads"": true, ""has_issues"": true, ""has_wiki"": true, ""language"": ""ASP"", ""name"": ""GitHookTest"", ""open_issues"": 0, ""organization"": ""KuduApps"", ""owner"": { ""email"": ""kuduapps@hotmail.com"", ""name"": ""KuduApps"" }, ""private"": false, ""pushed_at"": ""2012-06-28T00:11:48-07:00"", ""size"": 188, ""url"": ""https://github.com/KuduApps/SimpleWebApplication"", ""watchers"": 1 } }";
            string appName = "PullApiTestGitHubFormat";

            ApplicationManager.Run(appName, appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", githubPayload }
                };

                DeployPayloadHelper(appManager, client => 
                {
                    client.DefaultRequestHeaders.Add("X-Github-Event", "push");
                    return client.PostAsync("deploy", new FormUrlEncodedContent(post));
                });

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public void PullApiTestBitbucketFormat()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://github.com"", ""commits"": [ { ""author"": ""davidebbo"", ""branch"": ""master"", ""files"": [ { ""file"": ""Mvc3Application/Views/Home/Index.cshtml"", ""type"": ""modified"" } ], ""message"": ""Blah2\n"", ""node"": ""e550351c5188"", ""parents"": [ ""297fcc65308c"" ], ""raw_author"": ""davidebbo <david.ebbo@microsoft.com>"", ""raw_node"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-09-20 03:11:20"", ""utctimestamp"": ""2012-09-20 01:11:20+00:00"" } ], ""repository"": { ""absolute_url"": ""/KuduApps/SimpleWebApplication"", ""fork"": false, ""is_private"": false, ""name"": ""Mvc3Application"", ""owner"": ""davidebbo"", ""scm"": ""git"", ""slug"": ""mvc3application"", ""website"": """" }, ""user"": ""davidebbo"" }";
            string appName = "PullApiTestBitbucketFormat";

            ApplicationManager.Run(appName, appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                DeployPayloadHelper(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                    return client.PostAsync("deploy", new FormUrlEncodedContent(post));
                });

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public void PullApiTestBitbucketFormatWithMercurial()
        {
            string bitbucketPayload = @"{""canon_url"":""https://bitbucket.org"",""commits"":[{""author"":""pranavkm"",""branch"":""default"",""files"":[{""file"":""Hello.txt"",""type"":""modified""}],""message"":""Some more changes"",""node"":""0bbefd70c4c4"",""parents"":[""3cb8bf8aec0a""],""raw_author"":""Pranav <pranavkm@outlook.com>"",""raw_node"":""0bbefd70c4c4213bba1e91998141f6e861cec24d"",""revision"":4,""size"":-1,""timestamp"":""2012-12-17 19:41:28"",""utctimestamp"":""2012-12-17 18:41:28+00:00""}],""repository"":{""absolute_url"":""/kudutest/hellomercurial/"",""fork"":false,""is_private"":false,""name"":""HelloMercurial"",""owner"":""kudutest"",""scm"":""hg"",""slug"":""hellomercurial"",""website"":""""},""user"":""kudutest""}";
            string appName = "PullApiTestBitbucketFormatWithMercurial";

            ApplicationManager.Run(appName, appManager =>
            {
                var client = CreateClient(appManager);

                appManager.SettingsManager.SetValue("branch", "default").Wait();
                
                client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result.EnsureSuccessful();


                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }

        [Fact]
        public void PullApiTestBitbucketFormatWithPrivateMercurialRepository()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""Test-Branch"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Hello Mercurial! change"", ""node"": ""ee26963f2e54"", ""parents"": [ ""16ea3237dbcd"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""ee26963f2e54b9db5c0cd160600b29c4f7a7eff7"", ""revision"": 10, ""size"": -1, ""timestamp"": ""2012-12-24 18:22:14"", ""utctimestamp"": ""2012-12-24 17:22:14+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/privatemercurial/"", ""fork"": false, ""is_private"": true, ""name"": ""PrivateMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""privatemercurial"", ""website"": """" }, ""user"": ""kudutest"" }";
            string appName = "PullApiTestBitbucketFormatWithMercurial";

            ApplicationManager.Run(appName, appManager =>
            {
                if (!SshHelper.PrepareSSHEnv(appManager.SSHKeyManager))
                {
                    // Run SSH tests only if the key is present
                    return; 
                }
                var client = CreateClient(appManager);
                appManager.SettingsManager.SetValue("branch", "Test-Branch").Wait();

                client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result.EnsureSuccessful();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial!");
            });
        }

        [Fact]
        public void PullApiTestGitlabHQFormat()
        {
            string payload = @"{ ""before"":""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""after"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""ref"":""refs/heads/master"", ""user_id"":1, ""user_name"":""Remco Ros"", ""commits"":[ { ""id"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""message"":""Settings as content file"", ""timestamp"":""2012-11-11T14:32:02+01:00"", ""url"":""http://gitlab.proscat.nl/inspectbin/commits/4109312962bb269ecc3a0d7a3c82a119dcd54c8b"", ""author"":{ ""name"":""Remco Ros"", ""email"":""r.ros@proscat.nl"" }}], ""repository"":{ ""name"":""testing"", ""private"":false, ""url"":""https://github.com/KuduApps/SimpleWebApplication"", ""description"":null, ""homepage"":""https://github.com/KuduApps/SimpleWebApplication"" }}";
            string appName = "PullApiTestGitlabHQFormat";

            ApplicationManager.Run(appName, appManager =>
            {
                var client = CreateClient(appManager);
                client.PostAsync("deploy", new StringContent(payload)).Result.EnsureSuccessful();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitlabHQ", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public void PullApiTestCodebaseFormat()
        {
            string payload = @"{ ""before"":""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""after"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""ref"":""refs/heads/master"", ""repository"":{ ""name"":""testing"", ""public_access"":true, ""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"", ""clone_urls"": {""ssh"": ""git@codebasehq.com:test/test-repositories/git1.git"", ""http"": ""https://github.com/KuduApps/SimpleWebApplication""}}}";
            string appName = "PullApiTestCodebaseFormat";

            ApplicationManager.Run(appName, appManager =>
            {
                var client = CreateClient(appManager);
                client.DefaultRequestHeaders.Add("User-Agent", "Codebasehq.com");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result.EnsureSuccessful();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("CodebaseHQ", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public void PullApiTestKilnHgFormat()
        {
            string kilnPayload = @"{ ""commits"": [ { ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""message"": ""more fun text"", ""revision"": 20, ""tags"": [ ""tip"" ], ""timestamp"": ""1/16/2013 3:32:04 AM"", ""url"": ""https://13degrees.kilnhg.com/Code/Kudu-Public/Group/Site/History/d2415cbaa78e"" } ], ""pusher"": { ""accesstoken"": false, ""email"": ""xtorted@optonline.net"", ""fullName"": ""Brian Surowiec"" }, ""repository"": { ""central"": true, ""description"": """", ""id"": 113336, ""name"": ""Site"", ""url"": ""https://bitbucket.org/kudutest/hellomercurial/"" } }";
            string appName = KuduUtils.GetRandomWebsiteName("PullApiTestKilnHgFormat");

            ApplicationManager.Run(appName, appManager =>
            {
                var client = CreateClient(appManager);

                // since we're pulling against bitbucket we need to simulate a self-hosted setup of kiln
                appManager.SettingsManager.SetValue("kiln.domain", "bitbucket\\.org").Wait();
                appManager.SettingsManager.SetValue("branch", "default").Wait();

                var post = new Dictionary<string, string>
                {
                    { "payload", kilnPayload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result.EnsureSuccessful();

                var results = appManager.DeploymentManager.GetResultsAsync()
                                        .Result.ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Kiln", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }

        [Fact]
        public void PullApiTestGenericFormat()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""url"": ""https://github.com/KuduApps/SimpleWebApplication.git"", ""deployer"" : ""CodePlex"", ""branch"":""master""  }";
            string appName = "PullApiTestGenericFormat";

            ApplicationManager.Run(appName, appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                DeployPayloadHelper(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)));

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }

        [Fact]
        public void PullApiTestGenericFormatCustomBranch()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""ad21595c668f3de813463df17c04a3b23065fedc"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""test"" }";
            string appName = "PullApiTestGenericCustomBranch";

            ApplicationManager.Run(appName, appManager =>
            {
                appManager.SettingsManager.SetValue("branch", "test").Wait();

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                DeployPayloadHelper(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)));

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }

        [Fact]
        public void DeployingBranchThatExists()
        {
            string payload = @"{ ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""test"", newRef: ""ad21595c668f3de813463df17c04a3b23065fedc"" }";
            string appName = "DeployingBranchThatExists";

            ApplicationManager.Run(appName, appManager =>
            {
                appManager.SettingsManager.SetValue("branch", "test").Wait();

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };
                
                DeployPayloadHelper(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)));

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }

        [Fact]
        public void PullApiTestConsecutivePushesGetQueued()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""1ef30333deac14b99ac4bc93453cf4232ae88c24"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""master"" }";
            string appName = "PullApiTestPushesGetQueued";

            ApplicationManager.Run(appName, appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                // Start two requests at the same time, then wait for them
                // Ideally we'd push something else to github but at least this exercises the code path
                Task<HttpResponseMessage> responseTask1 = Task.Factory.StartNew(() => PostPayloadHelper(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post))));
                Task<HttpResponseMessage> responseTask2 = Task.Factory.StartNew(() => PostPayloadHelper(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post))));

                // One should be an OK and the other a Conflict. Which one is which can vary.
                if (responseTask1.Result.StatusCode == HttpStatusCode.Conflict)
                {
                    Assert.Equal(HttpStatusCode.OK, responseTask2.Result.StatusCode);
                }
                else
                {
                    Assert.Equal(HttpStatusCode.OK, responseTask1.Result.StatusCode);
                    Assert.Equal(HttpStatusCode.Conflict, responseTask2.Result.StatusCode);
                }

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Master branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }

        [Fact]
        public void PullApiTestSimpleFormat()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            ApplicationManager.Run(appName, appManager =>
            {
                DeployPayloadHelper(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Kudu");
            });
        }

        [Fact]
        public void PullApiTestSimpleFormatWithMercurial()
        {
            string payload = @"{""url"":""https://bitbucket.org/kudutest/hellomercurial/"",""format"":""basic"",""scm"":""hg""}";
            string appName = "PullApiTestSimpleFormatWithMercurial";

            ApplicationManager.Run(appName, appManager =>
            {
                var client = CreateClient(appManager);

                appManager.SettingsManager.SetValue("branch", "default").Wait();

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result.EnsureSuccessful();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Bitbucket", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }

        [Fact]
        public void PullApiTestSimpleFormatWithScmTypeNone()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            ApplicationManager.Run(appName, appManager =>
            {
                appManager.SettingsManager.SetValue(SettingsKeys.ScmType, ScmType.None.ToString()).Wait();

                DeployPayloadHelper(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Kudu");
            });
        }

        [Fact]
        public void PullApiTestRepoWithLongPath()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/suwatch/RepoWithLongPath.git";
            payload["format"] = "basic";
            string appName = "RepoWithLongPath";

            ApplicationManager.Run(appName, appManager =>
            {
                var exception = Assert.Throws<HttpUnsuccessfulRequestException>(() =>
                {
                    DeployPayloadHelper(appManager, client => client.PostAsJsonAsync("deploy", payload));
                });

                Assert.Contains("unable to create file symfony", exception.Message);

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Failed, results[0].Status);

                var entries = appManager.DeploymentManager.GetLogEntriesAsync(results[0].Id).Result.ToList();
                Assert.Equal(1, entries.Count);
                Assert.Equal("Fetching changes.", entries[0].Message);
                Assert.Equal(LogEntryType.Error, entries[0].Type);

                var details = appManager.DeploymentManager.GetLogEntryDetailsAsync(results[0].Id, entries[0].Id).Result.ToList();
                Assert.True(details.Count > 0, "must have at one log detail entry.");
                Assert.Contains("unable to create file symfony", details[0].Message);
                Assert.Equal(LogEntryType.Error, details[0].Type);

                // Must not have entry with "An unknown error has occurred"
                foreach (var detail in details)
                {
                    Assert.False(detail.Message.Contains("An unknown error has occurred"), "Must not contain unknow error!");
                }
            });
        }

        [Fact]
        public void DeployHookWithInvalidHttpMethod()
        {
            string appName = "HelloKudu";

            ApplicationManager.Run(appName, appManager =>
            {
                var client = CreateClient(appManager);

                HttpResponseMessage response = client.GetAsync("deploy").Result;
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                response = client.DeleteAsync("deploy").Result;
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                try
                {
                    response = client.PutAsync("deploy").Result;
                }
                catch (AggregateException ex)
                {
                    Assert.Contains("404", ex.InnerException.Message);
                }
            });
        }

        private static void DeployPayloadHelper(ApplicationManager appManager, Func<HttpClient, Task<HttpResponseMessage>> func, int retries = 3, int duration = 1000)
        {
            PostPayloadHelper(appManager, func, retries, duration).EnsureSuccessful().Dispose();
        }

        private static HttpResponseMessage PostPayloadHelper(ApplicationManager appManager, Func<HttpClient, Task<HttpResponseMessage>> func, int retries = 3, int duration = 1000)
        {
            while (retries > 0)
            {
                try
                {
                    using (HttpClient client = CreateClient(appManager))
                    {
                        HttpResponseMessage response = func(client).Result;

                        if (response.StatusCode == HttpStatusCode.InternalServerError)
                        {
                            response.EnsureSuccessful();
                        }

                        return response;
                    }
                }
                catch (HttpUnsuccessfulRequestException ex)
                {
                    if (ex.ResponseMessage.ExceptionMessage.Contains("403 while accessing https://github.com")
                     || ex.ResponseMessage.ExceptionMessage.Contains("Unknown SSL protocol error in connection to github.com")
                     || ex.ResponseMessage.ExceptionMessage.Contains("fatal: The remote end hung up unexpectedly"))
                    {
                        TestTracer.Trace("Retry due to github flakiness");

                        if (--retries > 0)
                        {
                            Thread.Sleep(duration);
                            continue;
                        }
                    }

                    throw;
                }
            }

            throw new InvalidOperationException("We should not reach here!");
        }

        private static HttpClient CreateClient(ApplicationManager appManager)
        {
            HttpClientHandler handler = HttpClientHelper.CreateClientHandler(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(appManager.ServiceUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        private class FakeMessageHandler : DelegatingHandler
        {
            public Uri Url { get; set; }

            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                Url = request.RequestUri;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
