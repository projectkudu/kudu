using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class DeploymentApisReturn404IfDeploymentIdDoesntExistTests
    {
        [Fact]
        public async Task DeploymentApisReturn404IfDeploymentIdDoesntExist()
        {
            string appName = "Rtn404IfDeployIdNotExist";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                string id = "foo";
                var ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeleteAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeployAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeployAsync(id, clean: true));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetLogEntriesAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetResultAsync(id));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, "fakeId"));
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);

                ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetDeploymentScriptAsync());
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
                Assert.Contains("Need to deploy website to get deployment script.", ex.ResponseMessage.ExceptionMessage);
            });
        }
    }

    [KuduXunitTestClass]
    public class DeploymentApisTests
    {
        [Fact]
        public async Task DeploymentApis()
        {
            // Arrange
            string appName = "DeploymentApis";

            using (var repo = Git.Clone("HelloWorld"))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    Assert.Equal(1, results.Count);
                    var result = results[0];
                    Assert.Equal("davidebbo", result.Author);
                    Assert.Equal("david.ebbo@microsoft.com", result.AuthorEmail);
                    Assert.True(result.Current);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.NotNull(result.Url);
                    Assert.NotNull(result.LogUrl);
                    Assert.True(String.IsNullOrEmpty(result.Deployer));

                    // Make sure we end up on the master branch
                    CommandResult commandResult = await appManager.CommandExecutor.ExecuteCommand("git status", @"site\repository");
                    Assert.Contains("On branch master", commandResult.Output);

                    ICredentials cred = appManager.DeploymentManager.Credentials;
                    KuduAssert.VerifyUrl(result.Url, cred);
                    KuduAssert.VerifyUrl(result.LogUrl, cred);

                    var resultAgain = await appManager.DeploymentManager.GetResultAsync(result.Id);
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
                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.Equal(2, results.Count);
                    string oldId = results[1].Id;

                    // Delete one
                    await appManager.DeploymentManager.DeleteAsync(oldId);

                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    Assert.Equal(1, results.Count);
                    Assert.NotEqual(oldId, results[0].Id);

                    result = results[0];

                    // Redeploy
                    await appManager.DeploymentManager.DeployAsync(result.Id);

                    // Clean deploy
                    await appManager.DeploymentManager.DeployAsync(result.Id, clean: true);

                    var entries = (await appManager.DeploymentManager.GetLogEntriesAsync(result.Id)).ToList();

                    Assert.True(entries.Count > 0);

                    // First entry is always null
                    Assert.Null(entries[0].DetailsUrl);

                    var entryWithDetails = entries.First(e => e.DetailsUrl != null);

                    var nested = (await appManager.DeploymentManager.GetLogEntryDetailsAsync(result.Id, entryWithDetails.Id)).ToList();

                    Assert.True(nested.Count > 0);

                    KuduAssert.VerifyLogOutput(appManager, result.Id, "Cleaning Git repository");

                    // Get deployment script
                    var stream = await appManager.DeploymentManager.GetDeploymentScriptAsync();
                    using (var zipFile = new ZipArchive(stream))
                    {
                        // Verify 2 files exist
                        Assert.Equal(2, zipFile.Entries.Count);
                    }

                    // Can't delete the active one
                    var ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeleteAsync(result.Id));
                    Assert.Equal(HttpStatusCode.Conflict, ex.ResponseMessage.StatusCode);

                    // Corrupt git repository by removing HEAD file from it
                    // And verify git repository is not identified
                    appManager.VfsManager.Delete("site\\repository\\.git\\HEAD");

                    var notFoundException = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.DeployAsync(null));

                    // Expect a not found failure as no repository is found (since the git repository is now corrupted)
                    Assert.Equal(HttpStatusCode.NotFound, notFoundException.ResponseMessage.StatusCode);

                    // Another got push should reinitialize the git repository
                    appManager.GitDeploy(repo.PhysicalPath);

                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    // Make sure running this again doesn't throw an exception
                    await appManager.DeploymentManager.DeployAsync(null);

                    // Create new deployment test
                    var id = Guid.NewGuid().ToString();
                    var payload = new JObject();
                    var endtime = DateTime.UtcNow;
                    payload["status"] = (int)DeployStatus.Success;
                    payload["message"] = "this is commit message";
                    payload["deployer"] = "kudu";
                    payload["author"] = "tester";
                    payload["end_time"] = endtime.ToString("o");
                    payload["details"] = "http://kudu.com/deployments/details";

                    // add new deployment
                    result = await appManager.DeploymentManager.PutAsync(id, payload);
                    Assert.Equal(id, result.Id);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.Equal("this is commit message", result.Message);
                    Assert.Equal("kudu", result.Deployer);
                    Assert.Equal("tester", result.Author);
                    Assert.Equal(endtime, result.EndTime);
                    Assert.Equal(true, result.Current);

                    // check result
                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.True(results.Any(r => r.Id == id));
                    result = results[0];
                    Assert.Equal(id, result.Id);
                    Assert.Equal(DeployStatus.Success, result.Status);
                    Assert.Equal("this is commit message", result.Message);
                    Assert.Equal("kudu", result.Deployer);
                    Assert.Equal("tester", result.Author);
                    Assert.Equal(endtime, result.EndTime);
                    Assert.Equal(true, result.Current);

                    entries = (await appManager.DeploymentManager.GetLogEntriesAsync(result.Id)).ToList();
                    Assert.Equal(1, entries.Count);
                    Assert.Equal("Deployment successful.", entries[0].Message);

                    entries = (await appManager.DeploymentManager.GetLogEntryDetailsAsync(result.Id, entries[0].Id)).ToList();
                    Assert.Equal(1, entries.Count);
                    Assert.Equal(payload["details"], entries[0].Message);
                });
            }
        }
    }

    [KuduXunitTestClass]
    public class DeploymentVerifyEtagTests
    {
        [Fact]
        public async Task DeploymentVerifyEtag()
        {
            string appName = "VerifyEtag";

            using (var repo = Git.Clone("HelloWorld"))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    EntityTagHeaderValue etag = null;
                    EntityTagHeaderValue etagWithQuery = null;

                    // no etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", null, HttpStatusCode.OK);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", null, HttpStatusCode.OK);
                    Assert.NotEqual(etag, etagWithQuery);

                    // match etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", etag, HttpStatusCode.NotModified);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", etagWithQuery, HttpStatusCode.NotModified);
                    Assert.NotEqual(etag, etagWithQuery);

                    appManager.GitDeploy(repo.PhysicalPath);

                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(results[0].Current);

                    // mismatch etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", etag, HttpStatusCode.OK);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", etagWithQuery, HttpStatusCode.OK);
                    Assert.NotEqual(etag, etagWithQuery);

                    // match etag
                    etag = await VerifyEtagAsync(appManager, "/deployments", etag, HttpStatusCode.NotModified);
                    etagWithQuery = await VerifyEtagAsync(appManager, "/deployments/?$top=1", etagWithQuery, HttpStatusCode.NotModified);
                    Assert.NotEqual(etag, etagWithQuery);
                });
            }
        }

        private async Task<EntityTagHeaderValue> VerifyEtagAsync(ApplicationManager appManager, string uri, EntityTagHeaderValue input, HttpStatusCode statusCode)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                if (input != null)
                {
                    request.Headers.IfNoneMatch.Add(input);
                }

                using (var response = await appManager.DeploymentManager.Client.SendAsync(request))
                {
                    Assert.Equal(statusCode, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        Assert.Equal(input, response.Headers.ETag);
                    }
                    else
                    {
                        Assert.NotEqual(input, response.Headers.ETag);
                    }

                    return response.Headers.ETag;
                }
            }
        }
    }

    [KuduXunitTestClass]
    public class DeploymentManagerExtensibilityTests
    {
        [Fact]
        public async Task DeploymentManagerExtensibility()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = "DeploymentApis";

            using (var repo = Git.Clone(repositoryName))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    var handler = new FakeMessageHandler()
                    {
                        InnerHandler = HttpClientHelper.CreateClientHandler(appManager.DeploymentManager.ServiceUrl, appManager.DeploymentManager.Credentials)
                    };

                    var manager = new RemoteDeploymentManager(appManager.DeploymentManager.ServiceUrl, appManager.DeploymentManager.Credentials, handler);
                    var results = (await manager.GetResultsAsync()).ToList();

                    Assert.Equal(0, results.Count);
                    Assert.NotNull(handler.Url);
                });
            }
        }
    }

    [KuduXunitTestClass]
    public class DeleteKuduSiteCleansProperlyTests
    {
        [Fact]
        public async Task DeleteKuduSiteCleansProperly()
        {
            string appName = "DeleteKuduSiteCleansProperly";
            // This file is part of HelloWorld repo
            string defaultHtmFile = "default.htm";
            // This file is part of HelloKudu repo
            string indexHtmFile = "index.htm";

            using (var repo = Git.Clone("HelloWorld"))
            {
                await ApplicationManager.RunAsync(appName, async appManager =>
                {
                    // Deploy HelloWorld repository
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    // Verify deployed properly
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);

                    // Verify default.htm file from HelloWorld exists
                    bool defaultHtmExists = appManager.VfsWebRootManager.Exists(defaultHtmFile);
                    Assert.True(defaultHtmExists, defaultHtmFile + " doesn't exist");

                    // Verify index.htm file does not exist
                    bool indexHtmExists = appManager.VfsWebRootManager.Exists(indexHtmFile);
                    Assert.False(indexHtmExists, indexHtmFile + " exist");

                    // Add file to wwwroot not through deployment/repository
                    string extraFileName = "extra.file";
                    appManager.VfsWebRootManager.WriteAllText(extraFileName, "extra content");

                    // Delete repository without removing wwwroot
                    await appManager.RepositoryManager.Delete();
                    results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                    // Verify deployments were cleaned
                    Assert.Equal(0, results.Count);

                    // Verify extra.file was not cleaned
                    bool extraFileExists = appManager.VfsWebRootManager.Exists(extraFileName);
                    Assert.True(extraFileExists, extraFileName + " doesn't exist");

                    // Verify default.htm was not cleaned
                    defaultHtmExists = appManager.VfsWebRootManager.Exists(defaultHtmFile);
                    Assert.True(defaultHtmExists, defaultHtmFile + " doesn't exist");

                    // Redeploy with new Repo
                    using (var newRepo = Git.Clone("HelloKudu"))
                    {
                        // Redeploy HelloKudu repository
                        appManager.GitDeploy(newRepo.PhysicalPath);
                        results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                        // Verify deployed properly
                        Assert.Equal(1, results.Count);
                        Assert.Equal(DeployStatus.Success, results[0].Status);
                        Assert.NotNull(results[0].LastSuccessEndTime);

                        // Verify default.htm file from HelloWorld does not exist
                        defaultHtmExists = appManager.VfsWebRootManager.Exists(defaultHtmFile);
                        Assert.False(defaultHtmExists, defaultHtmFile + " exist");

                        // Verify index.htm file from HelloKudu exists
                        indexHtmExists = appManager.VfsWebRootManager.Exists(indexHtmFile);
                        Assert.True(indexHtmExists, indexHtmFile + " doesn't exist");

                        // Verify extra.file there
                        extraFileExists = appManager.VfsWebRootManager.Exists(extraFileName);
                        Assert.True(extraFileExists, extraFileName + " doesn't exist");
                    }
                });
            }
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestGitHubFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestGitHubFormat()
        {
            string githubPayload = @"{ ""after"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""before"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"",  ""commits"": [ { ""added"": [], ""author"": { ""email"": ""prkrishn@hotmail.com"", ""name"": ""Pranav K"", ""username"": ""pranavkm"" }, ""id"": ""43acf30efa8339103e2bed5c6da1379614b00572"", ""message"": ""Changes from master again"", ""modified"": [ ""Hello.txt"" ], ""timestamp"": ""2012-12-17T17:32:15-08:00"" } ], ""compare"": ""https://github.com/KuduApps/GitHookTest/compare/7e2a599e2d28...7e2a599e2d28"", ""created"": false, ""deleted"": false, ""forced"": false, ""head_commit"": { ""added"": [ "".gitignore"", ""SimpleWebApplication.sln"", ""SimpleWebApplication/About.aspx"", ""SimpleWebApplication/About.aspx.cs"", ""SimpleWebApplication/About.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx"", ""SimpleWebApplication/Account/ChangePassword.aspx.cs"", ""SimpleWebApplication/Account/ChangePassword.aspx.designer.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.cs"", ""SimpleWebApplication/Account/ChangePasswordSuccess.aspx.designer.cs"", ""SimpleWebApplication/Account/Login.aspx"", ""SimpleWebApplication/Account/Login.aspx.cs"", ""SimpleWebApplication/Account/Login.aspx.designer.cs"", ""SimpleWebApplication/Account/Register.aspx"", ""SimpleWebApplication/Account/Register.aspx.cs"", ""SimpleWebApplication/Account/Register.aspx.designer.cs"", ""SimpleWebApplication/Account/Web.config"", ""SimpleWebApplication/Default.aspx"", ""SimpleWebApplication/Default.aspx.cs"", ""SimpleWebApplication/Default.aspx.designer.cs"", ""SimpleWebApplication/Global.asax"", ""SimpleWebApplication/Global.asax.cs"", ""SimpleWebApplication/Properties/AssemblyInfo.cs"", ""SimpleWebApplication/Scripts/jquery-1.4.1-vsdoc.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.js"", ""SimpleWebApplication/Scripts/jquery-1.4.1.min.js"", ""SimpleWebApplication/SimpleWebApplication.csproj"", ""SimpleWebApplication/Site.Master"", ""SimpleWebApplication/Site.Master.cs"", ""SimpleWebApplication/Site.Master.designer.cs"", ""SimpleWebApplication/Styles/Site.css"", ""SimpleWebApplication/Web.Debug.config"", ""SimpleWebApplication/Web.Release.config"", ""SimpleWebApplication/Web.config"" ], ""author"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""committer"": { ""email"": ""david.ebbo@microsoft.com"", ""name"": ""davidebbo"", ""username"": ""davidebbo"" }, ""distinct"": false, ""id"": ""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""message"": ""Initial"", ""modified"": [], ""removed"": [], ""timestamp"": ""2011-11-21T23:07:42-08:00"", ""url"": ""https://github.com/KuduApps/GitHookTest/commit/7e2a599e2d28665047ec347ab36731c905c95e8b"" }, ""pusher"": { ""name"": ""none"" }, ""ref"": ""refs/heads/foo/blah"", ""repository"": { ""created_at"": ""2012-06-28T00:07:55-07:00"", ""description"": """", ""fork"": false, ""forks"": 1, ""has_downloads"": true, ""has_issues"": true, ""has_wiki"": true, ""language"": ""ASP"", ""name"": ""GitHookTest"", ""open_issues"": 0, ""organization"": ""KuduApps"", ""owner"": { ""email"": ""kuduapps@hotmail.com"", ""name"": ""KuduApps"" }, ""private"": false, ""pushed_at"": ""2012-06-28T00:11:48-07:00"", ""size"": 188, ""url"": ""https://github.com/KuduApps/SimpleWebApplication"", ""watchers"": 1 } }";
            string appName = "PullApiTestGitHubFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "foo/blah");

                var post = new Dictionary<string, string>
                {
                    { "payload", githubPayload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("X-Github-Event", "push");
                    return client.PostAsync("deploy?scmType=GitHub", new FormUrlEncodedContent(post));
                }, isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestBitbucketFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestBitbucketFormat()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://github.com"", ""commits"": [ { ""author"": ""davidebbo"", ""branch"": ""master"", ""files"": [ { ""file"": ""Mvc3Application/Views/Home/Index.cshtml"", ""type"": ""modified"" } ], ""message"": ""Blah2\n"", ""node"": ""e550351c5188"", ""parents"": [ ""297fcc65308c"" ], ""raw_author"": ""davidebbo <david.ebbo@microsoft.com>"", ""raw_node"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""revision"": null, ""size"": -1, ""timestamp"": ""2012-09-20 03:11:20"", ""utctimestamp"": ""2012-09-20 01:11:20+00:00"" } ], ""repository"": { ""absolute_url"": ""/KuduApps/SimpleWebApplication"", ""fork"": false, ""is_private"": false, ""name"": ""Mvc3Application"", ""owner"": ""davidebbo"", ""scm"": ""git"", ""slug"": ""mvc3application"", ""website"": """" }, ""user"": ""davidebbo"" }";
            string appName = "PullApiTestBitbucketFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                await appManager.SettingsManager.SetValue(SettingsKeys.UseShallowClone, "true");

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                    return client.PostAsync("deploy?scmType=BitbucketGit", new FormUrlEncodedContent(post));
                }, isContinuous: true);

                var resultsTask = appManager.DeploymentManager.GetResultsAsync();
                var verifyUrl = KuduAssert.VerifyUrlAsync(appManager.SiteUrl, "Welcome to ASP.NET!");

                await Task.WhenAll(resultsTask, verifyUrl);

                var results = (await resultsTask).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestBitbucketFormatWithMercurialTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestBitbucketFormatWithMercurial()
        {
            string bitbucketPayload = @"{""canon_url"":""https://bitbucket.org"",""commits"":[{""author"":""pranavkm"",""branch"":""default"",""files"":[{""file"":""Hello.txt"",""type"":""modified""}],""message"":""Some more changes"",""node"":""0bbefd70c4c4"",""parents"":[""3cb8bf8aec0a""],""raw_author"":""Pranav <pranavkm@outlook.com>"",""raw_node"":""0bbefd70c4c4213bba1e91998141f6e861cec24d"",""revision"":4,""size"":-1,""timestamp"":""2012-12-17 19:41:28"",""utctimestamp"":""2012-12-17 18:41:28+00:00""}],""repository"":{""absolute_url"":""/kudutest/hellomercurial/"",""fork"":false,""is_private"":false,""name"":""HelloMercurial"",""owner"":""kudutest"",""scm"":""hg"",""slug"":""hellomercurial"",""website"":""""},""user"":""kudutest""}";
            string appName = "PullApiTestBitbucketFormatWithMercurial";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "default");

                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                    return client.PostAsync("deploy?scmType=BitbucketHg", new FormUrlEncodedContent(post));
                }, isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestBitbucketFormatWithPrivateMercurialRepositoryTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestBitbucketFormatWithPrivateMercurialRepository()
        {
            string bitbucketPayload = @"{ ""canon_url"": ""https://bitbucket.org"", ""commits"": [ { ""author"": ""pranavkm"", ""branch"": ""Test-Branch"", ""files"": [ { ""file"": ""Hello.txt"", ""type"": ""modified"" } ], ""message"": ""Hello Mercurial! change"", ""node"": ""ee26963f2e54"", ""parents"": [ ""16ea3237dbcd"" ], ""raw_author"": ""Pranav <pranavkm@outlook.com>"", ""raw_node"": ""ee26963f2e54b9db5c0cd160600b29c4f7a7eff7"", ""revision"": 10, ""size"": -1, ""timestamp"": ""2012-12-24 18:22:14"", ""utctimestamp"": ""2012-12-24 17:22:14+00:00"" } ], ""repository"": { ""absolute_url"": ""/kudutest/privatemercurial/"", ""fork"": false, ""is_private"": true, ""name"": ""PrivateMercurial"", ""owner"": ""kudutest"", ""scm"": ""hg"", ""slug"": ""privatemercurial"", ""website"": """" }, ""user"": ""kudutest"" }";
            string appName = "PullApiTestBitbucketFormatWithMercurial";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (!SshHelper.PrepareSSHEnv(appManager.SSHKeyManager))
                {
                    // Run SSH tests only if the key is present
                    return;
                }

                await appManager.SettingsManager.SetValue("branch", "Test-Branch");

                var post = new Dictionary<string, string>
                {
                    { "payload", bitbucketPayload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Bitbucket.org");
                    return client.PostAsync("deploy?scmType=BitbucketHg", new FormUrlEncodedContent(post));
                }, isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.True(results[0].Deployer.StartsWith("Bitbucket"));

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial!");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestGitlabHQFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestGitlabHQFormat()
        {
            string payload = @"{""before"": ""a224fc12d7d024812691aa047d5e365385143e83"",""after"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"",""ref"": ""refs/heads/master"",""checkout_sha"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"",""user_id"": 99630,""user_name"": ""Suwat Bodin"",""project_id"": 171383,""repository"": {""name"": ""SimpleWebApplication"",""url"": ""git@gitlab.com:KuduApps/SimpleWebApplication.git"",""description"": """",""homepage"": ""https://gitlab.com/KuduApps/SimpleWebApplication"",""git_http_url"": ""https://gitlab.com/KuduApps/SimpleWebApplication.git"",""git_ssh_url"": ""git@gitlab.com:KuduApps/SimpleWebApplication.git"",""visibility_level"": 20},""commits"": [{""id"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"",""message"": ""Settings as content file\n"",""timestamp"": ""2011-11-29T15:21:02-08:00"",""url"": ""https://gitlab.com/KuduApps/SimpleWebApplication/commit/ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"",""author"": {""name"": ""davidebbo"",""email"": ""david.ebbo@microsoft.com""}}],""total_commits_count"": 1}";
            string appName = "PullApiTestGitlabHQFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new StringContent(payload)), isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitlabHQ", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestCodebaseFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestCodebaseFormat()
        {
            string payload = @"{ ""before"":""7e2a599e2d28665047ec347ab36731c905c95e8b"", ""after"":""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""ref"":""refs/heads/master"", ""repository"":{ ""name"":""testing"", ""public_access"":true, ""url"":""http://test.codebasehq.com/projects/test-repositories/repositories/git1"", ""clone_urls"": {""ssh"": ""git@codebasehq.com:test/test-repositories/git1.git"", ""http"": ""https://github.com/KuduApps/SimpleWebApplication""}}}";
            string appName = "PullApiTestCodebaseFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Codebasehq.com");
                    return client.PostAsync("deploy", new FormUrlEncodedContent(post));
                }, isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("CodebaseHQ", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestKilnHgFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestKilnHgFormat()
        {
            string kilnPayload = @"{ ""commits"": [ { ""author"": ""Brian Surowiec <xtorted@optonline.net>"", ""branch"": ""default"", ""id"": ""0bbefd70c4c4213bba1e91998141f6e861cec24d"", ""message"": ""more fun text"", ""revision"": 20, ""tags"": [ ""tip"" ], ""timestamp"": ""1/16/2013 3:32:04 AM"", ""url"": ""https://13degrees.kilnhg.com/Code/Kudu-Public/Group/Site/History/d2415cbaa78e"" } ], ""pusher"": { ""accesstoken"": false, ""email"": ""xtorted@optonline.net"", ""fullName"": ""Brian Surowiec"" }, ""repository"": { ""central"": true, ""description"": """", ""id"": 113336, ""name"": ""Site"", ""url"": ""https://bitbucket.org/kudutest/hellomercurial/"" } }";
            string appName = "PullApiTestKilnHgFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // since we're pulling against bitbucket we need to simulate a self-hosted setup of kiln
                await appManager.SettingsManager.SetValue("kiln.domain", "bitbucket\\.org");
                await appManager.SettingsManager.SetValue("branch", "default");

                var post = new Dictionary<string, string>
                {
                    { "payload", kilnPayload }
                };

                await DeployPayloadHelperAsync(appManager, client =>
                {
                    return client.PostAsync("deploy", new FormUrlEncodedContent(post));
                }, isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Kiln", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestVsoFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestVsoFormat()
        {
            string payload = @"{ ""publisherId"": ""tfs"", ""resource"": { ""repository"": { ""remoteUrl"": ""https://github.com/KuduApps/HelloKudu"" } } }";
            string appName = "PullApiTestVsoFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await DeployPayloadHelperAsync(appManager, client =>
                {
                    return client.PostAsync("deploy?scmType=Vso", new StringContent(payload));
                }, isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("VSTS", results[0].Deployer);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Kudu");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestGenericFormatTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestGenericFormat()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""ea1c6d7ea669c816dd5f86206f7b47b228fdcacd"", ""url"": ""https://github.com/KuduApps/SimpleWebApplication.git"", ""deployer"" : ""CodePlex"", ""branch"":""master""  }";
            string appName = "PullApiTestGenericFormat";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)), isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("davidebbo", results[0].Author);
                Assert.Equal("david.ebbo@microsoft.com", results[0].AuthorEmail);
                Assert.Equal("Settings as content file", results[0].Message.Trim());
                Assert.Equal("ea1c6d7ea669c816dd5f86206f7b47b228fdcacd", results[0].Id);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET!");
                Assert.Equal("CodePlex", results[0].Deployer);

                // Verify the deployment status
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestGenericFormatCustomBranchTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestGenericFormatCustomBranch()
        {
            string payload = @"{ ""oldRef"": ""0000000000000000000"", ""newRef"": ""b4bd5b73ec4c15019d41d16e418c3017b70b3796"", ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""test"" }";
            string appName = "PullApiTestGenericCustomBranch";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "test");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)), isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }
    }

    [KuduXunitTestClass]
    public class DeployingBranchThatExistsTests : DeploymentManagerTests
    {
        [Fact]
        public async Task DeployingBranchThatExists()
        {
            string payload = @"{ ""url"": ""https://github.com/KuduApps/RepoWithMultipleBranches.git"", ""deployer"" : ""CodePlex"", branch: ""test"", newRef: ""ad21595c668f3de813463df17c04a3b23065fedc"" }";
            string appName = "DeployingBranchThatExists";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue("branch", "test");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                await DeployPayloadHelperAsync(appManager, client => client.PostAsync("deploy", new FormUrlEncodedContent(post)), isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                Assert.Equal("CodePlex", results[0].Deployer);
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestRepoCommitTextWithSpecialCharTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestRepoCommitTextWithSpecialChar()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/RepoCommitTextWithSpecialChar";
            payload["format"] = "basic";
            string appName = "RepoCommitTextWithSpecialChar";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Fetch master branch from first repo
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);
                // [0] => git.exe, [1] => LibGit2Sharp
                KuduAssert.EqualsAny(new [] {"invalid char is ", "invalid char is \n"}, results[0].Message);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello World");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestSimpleFormatWithAsyncTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestSimpleFormatWithAsync()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Fetch master branch from first repo
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy?isAsync=true", payload), isContinuous: true);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello Kudu</h1>");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestSimpleFormatMultiBranchWithUpdatesTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestSimpleFormatMultiBranchWithUpdates()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Fetch master branch from first repo
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello Kudu</h1>");

                // Switch to foo branch from first repo
                await appManager.SettingsManager.SetValue("branch", "foo");
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello Kudu - foo</h1>");

                // Fetch master branch from second repo to simulate update. It has one more commit over first repo
                payload["url"] = "https://github.com/KuduApps/HelloKudu2";
                await appManager.SettingsManager.SetValue("branch", "master");
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hello again Kudu</h1>");

                // Fetch foo branch from second repo to simulate update. It has a different commit that cannot be
                // fast-forwarded from the foo branch in the first repo
                await appManager.SettingsManager.SetValue("branch", "foo");
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                KuduAssert.VerifyUrl(appManager.SiteUrl, "<h1>Hi Kudu foo</h1>");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestSimpleFormatWithMercurialTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestSimpleFormatWithMercurial()
        {
            string payload = @"{""url"":""https://bitbucket.org/kudutest/hellomercurial/"",""format"":""basic"",""scm"":""hg""}";
            string appName = "PullApiTestSimpleFormatWithMercurial";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var client = CreateClient(appManager);

                await appManager.SettingsManager.SetValue("branch", "default");

                var post = new Dictionary<string, string>
                {
                    { "payload", payload }
                };

                (await client.PostAsync("deploy", new FormUrlEncodedContent(post))).EnsureSuccessful();

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Bitbucket", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl + "Hello.txt", "Hello mercurial");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestSimpleFormatWithScmTypeNoneTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestSimpleFormatWithScmTypeNone()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/HelloKudu";
            payload["format"] = "basic";
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue(SettingsKeys.ScmType, ScmType.None.ToString());

                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("GitHub", results[0].Deployer);

                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello Kudu");
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestGitSimpleFormatWithSpecificCommitIdTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestGitSimpleFormatWithSpecificCommitId()
        {
            await ApplicationManager.RunAsync("GitSimpleFormatWithBranch", async appManager =>
            {
                var gitUrl = "https://github.com/KuduApps/HelloKudu2.git";

                await PostDeploymentAndVerifyUrl(appManager, gitUrl + "#58063e4", false, DeployStatus.Success, "<h1>Hello again Kudu</h1>");

                await PostDeploymentAndVerifyUrl(appManager, gitUrl + "#2370e44", false, DeployStatus.Success, "<h1>Hello Kudu</h1>");

                await PostDeploymentAndVerifyUrl(appManager, gitUrl, false, DeployStatus.Success, "<h1>Hello again Kudu</h1>");

                var badRevision = Guid.NewGuid().ToString();
                var error = await KuduAssert.ThrowsUnwrappedAsync<HttpUnsuccessfulRequestException>(() => PostDeploymentAndVerifyUrl(appManager, gitUrl + "#" + badRevision, false, DeployStatus.Failed));
                Assert.Equal(HttpStatusCode.InternalServerError, error.ResponseMessage.StatusCode);
                Assert.Contains("Invalid revision '" + badRevision + "'!", error.ResponseMessage.ExceptionMessage);
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestHgSimpleFormatWithSpecificCommitIdTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestHgSimpleFormatWithSpecificCommitId()
        {
            await ApplicationManager.RunAsync("HgSimpleFormatWithBranch", async appManager =>
            {
                var hgUrl = "https://bitbucket.org/kudutest/hellomercurial";

                await appManager.SettingsManager.SetValue("branch", "default");

                await PostDeploymentAndVerifyUrl(appManager, hgUrl + "#e39d1ff", true, DeployStatus.Success, "Hello mercurial Commit 1", "/Hello.txt");

                await PostDeploymentAndVerifyUrl(appManager, hgUrl + "#478b0d4", true, DeployStatus.Success, "Hello mercurial Commit 2", "/Hello.txt");
                
                await PostDeploymentAndVerifyUrl(appManager, hgUrl, true, DeployStatus.Success, "Hello mercurial!", "/Hello.txt");

                var badRevision = Guid.NewGuid().ToString();
                var error = await KuduAssert.ThrowsUnwrappedAsync<HttpUnsuccessfulRequestException>(() => PostDeploymentAndVerifyUrl(appManager, hgUrl + "#" + badRevision, true, DeployStatus.Failed));
                Assert.Equal(HttpStatusCode.InternalServerError, error.ResponseMessage.StatusCode);
                Assert.Contains("Invalid revision '" + badRevision + "'!", error.ResponseMessage.ExceptionMessage);
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestRepoWithLongPathTests : DeploymentManagerTests
    {
        [Fact]
        public async Task PullApiTestRepoWithLongPath()
        {
            var payload = new JObject();
            payload["url"] = "https://github.com/KuduApps/RepoWithLongPath.git";
            payload["format"] = "basic";
            string appName = "RepoWithLongPath";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var exception = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(async () =>
                {
                    await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));
                });

                KuduAssert.ContainsAny(new[] { "unable to create file symfony", "The data area passed to a system call is too small" }, exception.Message);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Failed, results[0].Status);

                var entries = (await appManager.DeploymentManager.GetLogEntriesAsync(results[0].Id)).ToList();
                Assert.Equal(1, entries.Count);
                Assert.Equal("Fetching changes.", entries[0].Message);
                Assert.Equal(LogEntryType.Error, entries[0].Type);

                var details = (await appManager.DeploymentManager.GetLogEntryDetailsAsync(results[0].Id, entries[0].Id)).ToList();
                Assert.True(details.Count > 0, "must have at one log detail entry.");
                KuduAssert.ContainsAny(new[] { "unable to create file symfony", "The data area passed to a system call is too small" }, details[0].Message);
                Assert.Equal(LogEntryType.Error, details[0].Type);

                // Must not have entry with "An unknown error has occurred"
                foreach (var detail in details)
                {
                    Assert.False(detail.Message.Contains("An unknown error has occurred"), "Must not contain unknow error!");
                }
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestEmptyRepoTests : DeploymentManagerTests
    {
        [Theory]
        [InlineData("https://github.com/KuduApps/EmptyGitRepo", null)]
        [InlineData("https://bitbucket.org/kudutest/emptyhgrepo", "hg")]
        public async Task PullApiTestEmptyRepo(string url, string scm)
        {
            var payload = new JObject();
            payload["url"] = url;
            payload["format"] = "basic";
            if (!String.IsNullOrEmpty(scm))
            {
                payload["scm"] = scm;
            }

            string appName = "PullApiTestGitEmptyRepo";
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Fetch master branch from first repo
                await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(0, results.Count);
            });
        }
    }

    [KuduXunitTestClass]
    public class DeployHookWithInvalidHttpMethodTests : DeploymentManagerTests
    {
        [Fact]
        public async Task DeployHookWithInvalidHttpMethod()
        {
            string appName = "HelloKudu";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var client = CreateClient(appManager);

                HttpResponseMessage response = await client.GetAsync("deploy");

                // It's OK because it gets redirected to the Kudu root
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                response = await client.DeleteAsync("deploy");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                try
                {
                    response = await client.PutAsync("deploy");
                }
                catch (HttpRequestException ex)
                {
                    Assert.Contains("404", ex.Message);
                }
            });
        }
    }

    [KuduXunitTestClass]
    public class PullApiTestRepoInvalidUrlTests : DeploymentManagerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("hg")]
        public async Task PullApiTestRepoInvalidUrl(string scm)
        {
            Random random = new Random();
            string appName = "RepoInvalidUrl";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SSHKeyManager.GetPublicKey(ensurePublicKey: true);

                // Run per each scm in random order.
                foreach (var info in GetRepoInvalidInfos().Where(r => r.Scm == scm).OrderBy(r => random.Next()))
                {
                    TestTracer.Trace("Scenario: " + info);

                    // Test
                    await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(async () =>
                    {
                        await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", info.Payload));
                    });
                }
            });
        }

        readonly static string[] permissionDeniedExpectedMessage = new[]
        {
            "Permission denied [(]publickey[)]",
            "make sure you have the correct access rights"
        };

        private static IEnumerable<RepoInvalidInfo> GetRepoInvalidInfos()
        {
            yield return new RepoInvalidInfo("InvalidUrl", new [] {"Repository url 'InvalidUrl' is invalid."}, null);
            yield return new RepoInvalidInfo("InvalidUrl", new [] {"Repository url 'InvalidUrl' is invalid."}, null);
            yield return new RepoInvalidInfo(".", new [] {"Repository url '.' is invalid."}, null);
            yield return new RepoInvalidInfo("http://google.com/", new [] {"fatal:.*http://.*google.com.* not found", "\\[LibGit2SharpException: Too many redirects or authentication replays\\]"}, null);
            yield return new RepoInvalidInfo("http://google.com/", new [] {"abort: 'http://www.google.com/' does not appear to be an hg repository"}, "hg");
            yield return new RepoInvalidInfo("InvalidScheme://abcdefghigkl.com/", new [] {"fatal: Unable to find remote helper for 'InvalidScheme'"}, null);
            yield return new RepoInvalidInfo("InvalidScheme://abcdefghigkl.com/", new [] {"abort: repository InvalidScheme://abcdefghigkl.com/ not found"}, "hg");
            yield return new RepoInvalidInfo("http://abcdefghigkl.com/", new [] {"Could.*n.*t resolve host.*abcdefghigkl.com", "LibGit2SharpException: failed to send request: The server name or address could not be resolved", "LibGit2SharpException: Request failed with status code: 502"}, null);
            yield return new RepoInvalidInfo("http://abcdefghigkl.com/", new [] {"abort: error: getaddrinfo failed.*hg.exe pull"}, "hg");
            yield return new RepoInvalidInfo("https://abcdefghigkl.com/", new[] { "Could.*n.*t resolve host.*abcdefghigkl.com", "LibGit2SharpException: failed to send request: The server name or address could not be resolved", "LibGit2SharpException: Request failed with status code: 502"}, null);
            yield return new RepoInvalidInfo("https://abcdefghigkl.com/", new [] {"abort: error: getaddrinfo failed.*hg.exe pull"}, "hg");
            yield return new RepoInvalidInfo("git@abcdefghigkl.com:Invalid/Invalid.git", new [] { "no address associated with name", "Could not resolve hostname" }, null);
            yield return new RepoInvalidInfo("ssh://hg@abcdefghigkl.com/Invalid/Invalid.git", new [] {"abort: no suitable response from remote hg.*hg.exe pull"}, "hg");
            yield return new RepoInvalidInfo("git@github.com:Invalid/Invalid.git", permissionDeniedExpectedMessage, null);
            yield return new RepoInvalidInfo("git@bitbucket.org:Invalid/Invalid.git", permissionDeniedExpectedMessage, null);
            yield return new RepoInvalidInfo("git@github.com:KuduApps/Invalid.git", permissionDeniedExpectedMessage, null);
            yield return new RepoInvalidInfo("git@bitbucket.org:kudutest/Invalid.git", permissionDeniedExpectedMessage, null);
            yield return new RepoInvalidInfo("git@github.com:KuduApps/HelloKudu.git", permissionDeniedExpectedMessage, null);
            yield return new RepoInvalidInfo("git@bitbucket.org:kudutest/jeanprivate.git", permissionDeniedExpectedMessage, null);
            // due to unreliable error from github
            // yield return new RepoInvalidInfo("https://github.com/KuduApps/HelloKudu.git", "abort: HTTP Error 406: Not Acceptable.*hg.exe pull https://github.com/KuduApps/HelloKudu.git", "hg");
            yield return new RepoInvalidInfo("https://bitbucket.org/kudutest/hellomercurial/", new [] {"fatal:.*https://bitbucket.org/kudutest/hellomercurial.* not found", "\\[LibGit2SharpException: Request failed with status code: 404\\]"}, null);
            yield return new RepoInvalidInfo("https://github.com/Invalid/Invalid.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://github.com/KuduQAOrg/Invalid.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://github.com/KuduQAOrg/PrivateSubModule.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://KuduQAOrg@github.com/KuduQAOrg/PrivateSubModule.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://wrongusr@github.com/KuduQAOrg/PrivateSubModule.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://KuduQAOrg:wrongpwd@github.com/KuduQAOrg/PrivateSubModule.git", new [] {"fatal: Authentication failed.*git.exe fetch external", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://bitbucket.org/Invalid/Invalid.git", new [] {"fatal:.*https://bitbucket.org/Invalid/Invalid.git.* not found", "\\[LibGit2SharpException: Request failed with status code: 404\\]"}, null);
            yield return new RepoInvalidInfo("https://bitbucket.org/kudutest/Invalid.git", new [] {"fatal:.*https://bitbucket.org/kudutest/Invalid.git.* not found", "\\[LibGit2SharpException: Request failed with status code: 404\\]"}, null);
            yield return new RepoInvalidInfo("https://bitbucket.org/kudutest/jeanprivate.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://kudutest@bitbucket.org/kudutest/jeanprivate.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://wrongusr@bitbucket.org/kudutest/jeanprivate.git", new [] {"fatal: Authentication failed.*git.exe fetch", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
            yield return new RepoInvalidInfo("https://kudutest:wrongpwd@bitbucket.org/kudutest/jeanprivate.git", new [] {"fatal: Authentication failed.*git.exe fetch external", "\\[LibGit2SharpException: Request failed with status code: 401\\]"}, null);
        }

        public class RepoInvalidInfo
        {
            public RepoInvalidInfo(string url, IEnumerable<string> expect, string scm)
            {
                this.Url = url;
                this.Expect = expect;
                this.Scm = scm;
                this.Payload = new JObject();
                this.Payload["url"] = url;
                this.Payload["format"] = "basic";
                if (!String.IsNullOrEmpty(scm))
                {
                    this.Payload["scm"] = scm;
                }
            }

            public string Url { get; set; }
            public IEnumerable<string> Expect { get; set; }
            public string Scm { get; set; }
            public JObject Payload { get; set; }
            public override string ToString()
            {
                return String.Format("RepoInvalidInfo(url: \"{0},\" expect: \"{1}\", scm: \"{2}\")", this.Url, this.Expect, this.Scm);
            }
        }
    }

    public abstract class DeploymentManagerTests
    {
        internal async Task PostDeploymentAndVerifyUrl(ApplicationManager appManager, string url, bool isMercurial, DeployStatus status, string content = null, string path = null)
        {
            TestTracer.Trace("PostDeploymentAndVerifyUrl: {0}", url);

            var payload = new JObject();
            payload["url"] = url;
            payload["format"] = "basic";
            if (isMercurial)
            {
                payload["scm"] = "hg";
            }

            await DeployPayloadHelperAsync(appManager, client => client.PostAsJsonAsync("deploy", payload));

            var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
            Assert.True(results.Count > 0);
            Assert.Equal(status, results[0].Status);
            if (!String.IsNullOrEmpty(content))
            {
                KuduAssert.VerifyUrl(appManager.SiteUrl + path, content);
            }
        }

        internal static async Task DeployPayloadHelperAsync(ApplicationManager appManager, Func<HttpClient, Task<HttpResponseMessage>> func, bool isContinuous = false)
        {
            using (HttpClient client = CreateClient(appManager))
            {
                using (HttpResponseMessage response = await func(client))
                {
                    response.EnsureSuccessful();

                    if (isContinuous)
                    {
                        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

                        var location = response.Headers.Location;

                        // Poll till deployment finished
                        bool completed = false;
                        for (int i = 0; i < 60 && !completed; ++i)
                        {
                            if (location == null)
                            {
                                location = new Uri(new Uri(appManager.ServiceUrl), "api/deployments/latest");
                            }

                            await Task.Delay(1000);

                            using (var pending = await client.GetAsync(location))
                            {
                                pending.EnsureSuccessful();

                                if (pending.StatusCode == HttpStatusCode.OK)
                                {
                                    completed = true;
                                    break;
                                }

                                Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);

                                location = pending.Headers.Location;
                                Assert.NotNull(location);
                            }
                        }

                        Assert.True(completed, "the deployment is not completed within a given time!");
                    }
                    else
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    }
                }
            }
        }

        internal static HttpClient CreateClient(ApplicationManager appManager)
        {
            HttpClientHandler handler = HttpClientHelper.CreateClientHandler(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(appManager.ServiceUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        internal async Task WaitForAnyBuildingDeploymentAsync(ApplicationManager appManager)
        {
            bool deploying = false;
            int breakLoop = 0;
            do
            {
                Thread.Sleep(100);

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                deploying =
                    results != null &&
                    results.Any(r => r.Status == DeployStatus.Building);

                breakLoop++;
                if (breakLoop > 200)
                {
                    Assert.True(false, "No deployment result in pending state");
                }
            }
            while (!deploying);
        }
    }

    class FakeMessageHandler : DelegatingHandler
    {
        public Uri Url { get; set; }

        protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            Url = request.RequestUri;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
