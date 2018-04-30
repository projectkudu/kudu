using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Services.FetchHelpers;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class DropboxTests
    {
        private static readonly Random _random = new Random();

        public enum Scenario
        {
            Default,
            InPlace,
            NoRepository
        }

        [Theory]
        [InlineData(Scenario.Default)]
        [InlineData(Scenario.InPlace)]
        [InlineData(Scenario.NoRepository)]
        public async Task TestDropboxBasicForBasicScenario(Scenario scenario)
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            Assert.Equal(oauth.Account, account.email);
            var deploy = GetDeployInfo("/BasicTest", oauth, account);

            string appName = "DropboxTest";
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (scenario == Scenario.NoRepository)
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.NoRepository, "1");
                }
                else if (scenario == Scenario.InPlace)
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.RepositoryPath, "wwwroot");
                }

                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                var result = await client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy);
                result.EnsureSuccessful();

                await Task.WhenAll(
                    KuduAssert.VerifyUrlAsync(appManager.SiteUrl + "/default.html", "Hello Default!"),
                    KuduAssert.VerifyUrlAsync(appManager.SiteUrl + "/temp/temp.html", "Hello Temp!"),
                    KuduAssert.VerifyUrlAsync(appManager.SiteUrl + "/New Folder/New File.html", "Hello New File!")
                );

                var repositoryGit = appManager.VfsManager.Exists(@"site\repository\.git");
                var wwwrootGit = appManager.VfsManager.Exists(@"site\wwwroot\.git");

                if (scenario == Scenario.NoRepository)
                {
                    Assert.False(repositoryGit, @"site\repository\.git should not exist for " + scenario);
                    Assert.False(wwwrootGit, @"site\wwwroot\.git should not exist for " + scenario);
                }
                else if (scenario == Scenario.InPlace)
                {
                    Assert.False(repositoryGit, @"site\repository\.git should not exist for " + scenario);
                    Assert.True(wwwrootGit, @"site\wwwroot\.git should exist for " + scenario);
                }
                else if (scenario == Scenario.Default)
                {
                    Assert.True(repositoryGit, @"site\repository\.git should exist for " + scenario);
                    Assert.False(wwwrootGit, @"site\wwwroot\.git should not exist for " + scenario);
                }
            });
        }

        [Fact]
        public void TestDropboxSpecialChars()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            var deploy = GetDeployInfo("/SpecialCharsTest", oauth, account);

            string appName = "SpecialCharsTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy).Result.EnsureSuccessful();
            });
        }

        [Fact]
        public void TestDropboxSpecialFolderChars()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            var deploy = GetDeployInfo("/!@#$faiztest$#!@", oauth, account);

            string appName = "SpecialCharsTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                client.PostAsJsonAsync("deploy?scmType=Dropbox", deploy).Result.EnsureSuccessful();
            });
        }

        [Theory]
        [InlineData(60, 300)]
        public async Task TestDropboxRateLimiter(int limit, int total)
        {
            var interval = 1;
            var rateLimiter = new RateLimiter(limit, TimeSpan.FromSeconds(interval));
            var duration = TimeSpan.FromSeconds(total / limit - interval - 0.5);
            var start = DateTime.Now;
            while (--total > 0)
            {
                await rateLimiter.ThrottleAsync();
            }

            // Assert
            var end = DateTime.Now;
            Assert.True((end - start) >= duration, (end - start) + "<" + duration);
        }

        [Fact]
        public void TestDropboxConcurrent()
        {
            OAuthInfo oauth = GetOAuthInfo();
            if (oauth == null)
            {
                // only run in private kudu
                return;
            }

            AccountInfo account = GetAccountInfo(oauth);
            var deploy = GetDeployInfo("/BasicTest", oauth, account);

            string appName = "DropboxTest";
            ApplicationManager.Run(appName, appManager =>
            {
                HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
                var tasks = new Task<HttpResponseMessage>[]
                {
                    client.PostAsJsonAsync("deploy", deploy),
                    client.PostAsJsonAsync("deploy", deploy)
                };

                Task.WaitAll(tasks);

                // both 200 and 202 is success
                tasks[0].Result.EnsureSuccessful();
                tasks[1].Result.EnsureSuccessful();

                var success = (tasks[0].Result.StatusCode == HttpStatusCode.OK) ? tasks[0].Result : tasks[1].Result;
                var failure = !Object.ReferenceEquals(success, tasks[0].Result) ? tasks[0].Result : tasks[1].Result;

                success.EnsureSuccessful();
                Assert.Equal(HttpStatusCode.Accepted, failure.StatusCode);

                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                Assert.Equal("Dropbox", results[0].Deployer);
            });
        }

        internal OAuthInfo GetOAuthInfo(string appname = "")
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var reader = new JsonTextReader(new StreamReader(assembly.GetManifestResourceStream(String.Concat("Kudu.FunctionalTests.dropbox.", appname, "oauth.json")))))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<OAuthInfo>(reader);
            }
        }

        internal AccountInfo GetAccountInfo(OAuthInfo oauth)
        {
            var uri = new Uri("https://api.dropboxapi.com/2/users/get_current_account");
            var client = GetDropboxClient(uri, oauth);
            var response = client.PostAsync(uri.PathAndQuery, null).Result.EnsureSuccessful();
            using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<AccountInfo>(reader);
            }
        }

        internal JObject GetDeployInfo(string path, OAuthInfo oauth, AccountInfo account, string cursor = null)
        {
            var payload = new JObject();
            payload["dropbox_version"] = "2";
            payload["dropbox_token"] = oauth.Token;
            payload["dropbox_path"] = path;
            payload["dropbox_username"] = account.display_name;
            payload["dropbox_email"] = account.email;

            return payload;
        }

        private HttpClient GetDropboxClient(Uri uri, OAuthInfo oauth)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(string.Format("{0}://{1}", uri.Scheme, uri.Host));
            client.DefaultRequestHeaders.Add("user-agent", "Kudu.FunctionalTests/1.0");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauth.Token);
            return client;
        }

        public class OAuthInfo
        {
            public string Account { get; set; }
            public string Token { get; set; }
        }

        public class AccountInfo
        {
            public string display_name { get; set; }
            public string email { get; set; }
        }
    }
}

