using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class DeploymentManagerTests
    {
        [Fact]
        public void DeploymentApisReturn404IfDeploymentIdDoesntExist()
        {
            string appName = KuduUtils.GetRandomWebsiteName("Rtn404IfDeployIdNotExist");

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
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("DeploymentApis");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(1, results.Count);
                    var result = results[0];
                    Assert.Equal("Raquel Almeida", result.Author);
                    Assert.Equal("raquel_soares@msn.com", result.AuthorEmail);
                    Assert.True(result.Current);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.NotNull(result.Url);
                    Assert.NotNull(result.LogUrl);
                    Assert.True(String.IsNullOrEmpty(result.Deployer));

                    ICredentials cred = appManager.DeploymentManager.Credentials;
                    KuduAssert.VerifyUrl(result.Url, cred);
                    KuduAssert.VerifyUrl(result.LogUrl, cred);

                    var resultAgain = appManager.DeploymentManager.GetResultAsync(result.Id).Result;
                    Assert.Equal("Raquel Almeida", resultAgain.Author);
                    Assert.Equal("raquel_soares@msn.com", resultAgain.AuthorEmail);
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

                    KuduAssert.VerifyLogOutput(appManager, result.Id, "Cleaning git repository");

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
            string appName = KuduUtils.GetRandomWebsiteName("DeploymentApis");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    var handler = new FakeMessageHandler()
                    {
                        InnerHandler = new HttpClientHandler()
                    };

                    var manager = new RemoteDeploymentManager(appManager.DeploymentManager.ServiceUrl, handler);
                    manager.Credentials = appManager.DeploymentManager.Credentials;
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
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("DeleteKuduSiteCleansEverything");

            using (var repo = Git.CreateLocalRepository(repositoryName))
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
            string githubPayload = @"{ ""after"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""before"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""commits"": [], ""compare"": ""https://github.com/KuduApps/GitHookTest/compare/7e2a599e2d28...7e2a599e2d28"", ""created"": false, ""deleted"": false, ""forced"": false, ""head_commit"": { ""added"": [ "".gitignore"", ""SimpleWebApplication.sln"", ""SimpleWebApplication/About.aspx"", ""SimpleWebApplication/About.aspx.cs"", ""SimpleWebApplication/About.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx"", ""SimpleWebApplication/Account/ChangePassword.aspx.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.designer.cs"", ""SimpleWebApplication/Account/Login.aspx"", ""SimpleWebApplication/Account/Login.aspx.cs"", ""SimpleWebApplication/Account/Login.aspx.designer.cs"", ""SimpleWebApplication/Account/Register.aspx"", ""SimpleWebApplication/Account/Register.aspx.cs"", ""SimpleWebApplication/Account/Register.aspx.designer.cs"", ""SimpleWebApplication/Account/Web.config"", ""SimpleWebApplication/Default.aspx"", ""SimpleWebApplication/Default.aspx.cs"", ""SimpleWebApplication/Default.aspx.designer.cs"", ""SimpleWebApplication/Global.asax"", ""SimpleWebApplication/Global.asax.cs"", ""SimpleWebApplication/Properties/AssemblyInfo.cs"", ""SimpleWebApplication/Scripts/jquery-1.4.1-vsdoc.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.min.js"", ""SimpleWebApplication/SimpleWebApplication.csproj"", ""SimpleWebApplication/Site.Master"", ""SimpleWebApplication/Site.Master.cs"", ""SimpleWebApplication/Site.Master.designer.cs"", ""SimpleWebApplication/Styles/Site.css"", ""SimpleWebApplication/Web.Debug.config"", ""SimpleWebApplication/Web.Release.config"", ""SimpleWebApplication/Web.config"" ], ""author"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""committer"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""distinct"": false, ""id"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""message"": ""Initial"", ""modified"": [], ""removed"": [], ""timestamp"": ""2011-11-21T23:07:42-08:00"", ""url"": ""https://github.com/KuduApps/GitHookTest/commit/7e2a599e2d28665047ec347ab36731c905c95e8b"" }, ""pusher"": { ""name"": ""none"" }, ""ref"": ""refs/heads/master"", ""repository"": { ""created_at"": ""2012-06-28T00:07:55-07:00"", ""description"": """", ""fork"": false, ""forks"": 1, ""has_downloads"": true, ""has_issues"": true, ""has_wiki"": true, ""language"": ""ASP"", ""name"": ""GitHookTest"", ""open_issues"": 0, ""organization"": ""KuduApps"", ""owner"": { ""email"": ""kuduapps@hotmail.com"", ""name"": ""KuduApps"" }, ""private"": false, ""pushed_at"": ""2012-06-28T00:11:48-07:00"", ""size"": 188, ""url"": ""https://github.com/KuduApps/SimpleWebApplication"", ""watchers"": 1 } }";
            string appName = KuduUtils.GetRandomWebsiteName("PullApiTestGitHubFormat");

            ApplicationManager.Run(appName, appManager =>
            {
                var handler = new HttpClientHandler
                {
                    Credentials = appManager.DeploymentManager.Credentials
                };

                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(appManager.ServiceUrl),
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var post = new Dictionary<string, string>
                {
                    { "payload", githubPayload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Wait();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }

        [Fact]
        public void PullApiTestGenericFormat()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""url"": ""https://github.com/KuduApps/SimpleWebApplication.git"", ""deployer"" : ""me!"" }";
            string appName = KuduUtils.GetRandomWebsiteName("PullApiTestGenericFormat");

            ApplicationManager.Run(appName, appManager =>
            {
                var handler = new HttpClientHandler
                {
                    Credentials = appManager.DeploymentManager.Credentials
                };

                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(appManager.ServiceUrl),
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Wait();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
                Assert.Equal("me!", results[0].Deployer);
            });
        }

        [Fact]
        public void PullApiTestGenericFormatCustomBranch()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""ad21595c668f3de813463df17c04a3b23065fedc"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""me!"", branch: ""test"" }";
            string appName = KuduUtils.GetRandomWebsiteName("PullApiTestGenericCustomBranch");

            ApplicationManager.Run(appName, appManager =>
            {
                var handler = new HttpClientHandler
                {
                    Credentials = appManager.DeploymentManager.Credentials
                };

                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(appManager.ServiceUrl),
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                appManager.SettingsManager.SetValue("branch", "test").Wait();
                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Wait();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("me!", results[0].Deployer);
            });
        }

        [Fact]
        public void DeployingBranchThatExists()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""ad21595c668f3de813463df17c04a3b23065fedc"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""me!"", branch: ""test"" }";
            string appName = KuduUtils.GetRandomWebsiteName("DeployingBranchThatExists");

            ApplicationManager.Run(appName, appManager =>
            {
                var handler = new HttpClientHandler
                {
                    Credentials = appManager.DeploymentManager.Credentials
                };

                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(appManager.ServiceUrl),
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Wait();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Master branch");
                Assert.Equal("me!", results[0].Deployer);
            });
        }

        [Fact]
        public void PullApiTestConsecutivePushesGetQueued()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""ad21595c668f3de813463df17c04a3b23065fedc"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""me!"", branch: ""test"" }";
            string appName = KuduUtils.GetRandomWebsiteName("PullApiTestPushesGetQueued");

            ApplicationManager.Run(appName, appManager =>
            {
                var handler = new HttpClientHandler
                {
                    Credentials = appManager.DeploymentManager.Credentials
                };

                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(appManager.ServiceUrl),
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                Task<HttpResponseMessage> responseTask = null;

                // Do another post in parallel a second after
                new Thread(() =>
                {
                    Thread.Sleep(1000);
                    
                    // Ideally we'd push something else to github but at least this exercises the code path
                    responseTask = client.PostAsync("deploy", new FormUrlEncodedContent(post));

                }).Start();

                client.PostAsync("deploy", new FormUrlEncodedContent(post)).Wait();

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.NotNull(responseTask);
                Assert.Equal(HttpStatusCode.Conflict, responseTask.Result.StatusCode);
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Master branch");
                Assert.Equal("me!", results[0].Deployer);
            });
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
