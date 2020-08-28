using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class ZipDeploymentTests
    {
        [Fact]
        public Task TestSimpleZipDeployment()
        {
            return ApplicationManager.RunAsync("TestSimpleZipDeployment", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                var response = await DeployZip(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");
            });
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public Task TestSimpleZipUrlDeployment(bool isArmRequest, bool isAsync)
        {
            var testName = isArmRequest ? "TestSimpleZipUrlARMDeployment" : "TestSimpleZipUrlDeployment";
            var packageUri = "https://raw.githubusercontent.com/KuduApps/ZipDeployArtifacts/master/HelloKudu.zip";
            return ApplicationManager.RunAsync(testName, async appManager =>
            {
                var client = appManager.ZipDeploymentManager.Client;
                var requestUri = client.BaseAddress;
                if (!isArmRequest && isAsync)
                {
                    requestUri = new Uri(string.Format("{0}?isAsync=true", requestUri));
                }

                using (var request = new HttpRequestMessage(HttpMethod.Put, requestUri))
                {
                    if (isArmRequest)
                    {
                        var payload = new { properties = new { packageUri = packageUri } };
                        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                        request.Headers.Referrer = new Uri("https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites/site-name/extensions/zipdeploy?api-version=2016-03-01");
                        request.Headers.Add("x-ms-geo-location", "westus");
                    }
                    else
                    {
                        var payload = new { packageUri = packageUri };
                        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    }

                    using (var response = await client.SendAsync(request))
                    {
                        if (isAsync || isArmRequest)
                        {
                            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                            await AssertSuccessfulDeployment(appManager, timeoutSecs: 10);
                        }
                        else
                        {
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        }

                        await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new[] { "host.json", "images", "index.htm" }, "site/wwwroot");
                        await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new[] { "picture1.jpg", "picture2.jpg" }, path: "site/wwwroot/images");
                    }
                }
            });
        }

        [Fact]
        public Task TestZipDeployWithMetadata()
        {
            var author = "testAuthor";
            var authorEmail = "email@example.com";
            var deployer = "testDeployer";
            var message = "Test message for deployment";

            return ApplicationManager.RunAsync("TestSimpleZipDeployment", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                var response = await DeployZip(appManager, files, new ZipDeployMetadata
                {
                    Author = author,
                    AuthorEmail = authorEmail,
                    Deployer = deployer,
                    Message = message
                });
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");
                var result = await appManager.DeploymentManager.GetResultAsync("latest");

                Assert.Equal(author, result.Author);
                Assert.Equal(authorEmail, result.AuthorEmail);
                Assert.Equal(deployer, result.Deployer);
                Assert.Equal(message, result.Message);
            });
        }

        [Fact]
        public Task TestAsyncZipDeployment()
        {
            return ApplicationManager.RunAsync("TestAsyncZipDeployment", async appManager =>
            {
                // Big enough to require at least a couple polls for status until success
                var files = DeploymentTestHelper.CreateRandomFilesForZip(1000);
                var response = await DeployZip(appManager, files, new ZipDeployMetadata { IsAsync = true });
                response.EnsureSuccessStatusCode();

                TestTracer.Trace("Confirming deployment is in progress");

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal(GetZipDeployer(), result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");
            });
        }

        [Fact]
        public Task TestEmptyZipClearsWwwroot()
        {
            return ApplicationManager.RunAsync("TestEmptyZipClearsWwwroot", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                var response = await DeployZip(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");

                var empty = new TestFile[0];
                var response2 = await DeployZip(appManager, empty, new ZipDeployMetadata());
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[0], "site/wwwroot");
            });
        }

        [Fact]
        public Task EnsureConflictResultOnSimultaneousAsyncRequest()
        {
            return ApplicationManager.RunAsync("EnsureConflictResultOnSimultaneousAsyncRequest", async appManager =>
            {
                // Big enough to run for a few seconds
                var files = DeploymentTestHelper.CreateRandomFilesForZip(1000);
                var response = await DeployZip(appManager, files, new ZipDeployMetadata { IsAsync = true });
                response.EnsureSuccessStatusCode();

                // Immediately try another deployment
                var response2 = await DeployZip(appManager, new TestFile[0], new ZipDeployMetadata { IsAsync = true });

                Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal(GetZipDeployer(), result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");
            });
        }

        [Fact]
        public Task EnsureConflictResultOnSimultaneousSyncRequest()
        {
            return ApplicationManager.RunAsync("EnsureConflictResultOnSimultaneousSyncRequest", async appManager =>
            {
                // Big enough to run for a few seconds
                var files = DeploymentTestHelper.CreateRandomFilesForZip(1000);
                var response = await DeployZip(appManager, files, new ZipDeployMetadata { IsAsync = true });
                response.EnsureSuccessStatusCode();

                // Immediately try another deployment
                var response2 = await DeployZip(appManager, new TestFile[0], new ZipDeployMetadata());

                Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal(GetZipDeployer(), result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");
            });
        }

        [Fact]
        public Task EnsureExpectedKudusyncBehavior()
        {
            return ApplicationManager.RunAsync("EnsureExpectedKudusyncBehavior", async appManager =>
            {
                var origTime = DateTime.Now;
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                foreach (var file in files)
                {
                    file.ModifiedTime = origTime;
                }

                var response = await DeployZip(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByContent(appManager, files);

                // Deploy a new zip with some new files, some modified files, and some old files
                var newFiles = DeploymentTestHelper.CreateRandomFilesForZip(3);
                var oldfile = files[0];
                var modifiedFile = new TestFile { Filename = files[1].Filename, Content = "UPDATED", ModifiedTime = DateTime.Now };
                var files2 = newFiles.Concat(new[] { oldfile, modifiedFile }).ToArray();

                var response2 = await DeployZip(appManager, files2, new ZipDeployMetadata());
                response2.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByContent(appManager, files2);

                // Would need to check the trace log to ensure that the old file wasn't actually copied, but preserved.
            });
        }

        [Fact]
        public Task EnsureProjectSettingHonored()
        {
            return ApplicationManager.RunAsync("EnsureProjectSettingHonored", async appManager =>
            {
                await appManager.SettingsManager.SetValue("PROJECT", "subdir");

                var rootFile = new TestFile { Filename = "should-not-be-deployed", Content = "" };
                var subdirFile = new TestFile { Filename = "subdir/should-be-deployed", Content = "" };

                var files = new[] { rootFile, subdirFile };
                var response = await DeployZip(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new[] { "should-be-deployed" }, "site/wwwroot");
            });
        }

        [Fact]
        public Task EnsureBuildConfigBehavior()
        {
            return ApplicationManager.RunAsync("EnsureProjectSettingHonored", async appManager =>
            {
                var files = new[]
                {
                    new TestFile { Filename = "package.json", Content = simplePackageJsonWithDependencyContent}
                };

                // Should do KuduSync copy only, no build (zip deployments are assumed to be complete by default)
                var response = await DeployZip(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");

                var files2 = files.Concat(new[]
                {
                    new TestFile { Filename = ".deployment", Content = "[config]\r\nSCM_DO_BUILD_DURING_DEPLOYMENT = true"}
                }).ToArray();

                await appManager.SettingsManager.SetValue("SCM_DO_BUILD_DURING_DEPLOYMENT", "false");

                // Still won't build, since the site setting should override the .deployment. Note that we don't
                // expect .deployment to be copied to the site root.
                var response2 = await DeployZip(appManager, files2, new ZipDeployMetadata());
                response2.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");

                await appManager.SettingsManager.Delete("SCM_DO_BUILD_DURING_DEPLOYMENT");

                // Deploy once more, now we expect a node build to occur
                var response3 = await DeployZip(appManager, files2, new ZipDeployMetadata());
                response3.EnsureSuccessStatusCode();

                var expected = files.Concat(new[] { new TestFile { Filename = "node_modules" } }).ToArray();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expected.Select(f => f.Filename).ToArray(), "site/wwwroot");
            });
        }

        [Fact]
        public Task TestSideBySideWithGitRepo()
        {
            return ApplicationManager.RunAsync("TestSimpleZipDeployment", async appManager =>
            {
                using (var repo = Git.Init(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())))
                {
                    // First deploy zip
                    var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    var response = await DeployZip(appManager, files, new ZipDeployMetadata());
                    response.EnsureSuccessStatusCode();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot");

                    await appManager.SettingsManager.SetValue(SettingsKeys.ScmType, "LocalGit");

                    // Push-deploy git repo
                    var gitFilePath = Path.Combine(repo.PhysicalPath, Path.GetRandomFileName());
                    File.WriteAllText(gitFilePath, Guid.NewGuid().ToString("N"));
                    Git.Commit(repo.PhysicalPath, "Initial commit");
                    var gitResult = appManager.GitDeploy(repo.PhysicalPath);
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, Directory.GetFiles(repo.PhysicalPath).Select(f => Path.GetFileName(f)).ToArray(), "site/wwwroot");

                    // Deploy new zip
                    var files2 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    var response2 = await DeployZip(appManager, files2, new ZipDeployMetadata());
                    response2.EnsureSuccessStatusCode();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files2.Select(f => f.Filename).ToArray(), "site/wwwroot");

                    // Redeploy git deployment
                    var id = Git.Id(repo.PhysicalPath);
                    var response3 = await appManager.DeploymentManager.DeployAsync(id);
                    response3.EnsureSuccessStatusCode();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, Directory.GetFiles(repo.PhysicalPath).Select(f => Path.GetFileName(f)).ToArray(), "site/wwwroot");
                }
            });
        }

        [Fact]
        public Task TestSimpleWarDeployment()
        {
            return ApplicationManager.RunAsync("TestSimpleWarDeployment", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                var response = await DeployWar(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot/webapps/ROOT");
            });
        }

        [Fact]
        public Task TestWarDeploymentUpdatesWebXmlTimestamp()
        {
            return ApplicationManager.RunAsync("TestWarDeploymentUpdatesWebXmlTimestamp", async appManager =>
            {
                var files = new[] { new TestFile { Filename = "WEB-INF/web.xml" } };

                // STEP 1: Deploy WAR and get web.xml timestamp

                // Deploy WAR
                var response = await DeployWar(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();

                // Query web.xml timestamp
                var deployedFiles = appManager.VfsWebRootManager.ListAsync("webapps/ROOT/WEB-INF").Result.ToList();

                // Basic validation that web.xml exists
                Assert.Equal(1, deployedFiles.Count);
                Assert.Equal("web.xml", deployedFiles[0].Name);

                var creationTime = deployedFiles[0].CRTime;
                var modifiedTime = deployedFiles[0].MTime;

                // STEP 2: Get web.xml timestamp without deploying WAR again

                // Query web.xml timestamp
                deployedFiles = appManager.VfsWebRootManager.ListAsync("webapps/ROOT/WEB-INF").Result.ToList();

                // Basic validation that web.xml exists
                Assert.Equal(1, deployedFiles.Count);
                Assert.Equal("web.xml", deployedFiles[0].Name);

                // Check creation time and modification time haven't changed
                Assert.Equal(creationTime, deployedFiles[0].CRTime);
                Assert.Equal(modifiedTime, deployedFiles[0].MTime);

                // STEP 3: Deploy WAR again and get web.xml timestamp

                // Deploy WAR again
                response = await DeployWar(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();

                // Query web.xml timestamp
                deployedFiles = appManager.VfsWebRootManager.ListAsync("webapps/ROOT/WEB-INF").Result.ToList();

                // Basic validation that web.xml exists
                Assert.Equal(1, deployedFiles.Count);
                Assert.Equal("web.xml", deployedFiles[0].Name);

                // Check creation time hasn't changed, but modification time has been updated
                Assert.Equal(creationTime, deployedFiles[0].CRTime);
                Assert.NotEqual(modifiedTime, deployedFiles[0].MTime);
            });
        }

        [Fact]
        public Task TestSimpleWarDeploymentPerformsCleanDeployment()
        {
            return ApplicationManager.RunAsync("TestSimpleWarDeploymentPerformsCleanDeployment", async appManager =>
            {
                // STEP 1: create a file before doing a wardeploy. We expect wardeploy to remove this file
                var fileName = "file-before-wardeployment-" + Guid.NewGuid().ToString("N");
                appManager.VfsWebRootManager.WriteAllText("webapps/ROOT/" + fileName, "some content");
                var deployedFiles = appManager.VfsWebRootManager.ListAsync("webapps/ROOT/").Result.ToList();
                Assert.Equal(1, deployedFiles.Count);
                Assert.Equal(fileName, deployedFiles[0].Name);

                // STEP 2: Perform wardeploy and check files deployed by wardeploy are the only ones that exist now
                // (in other words, check that the file created in step 1 is removed by wardeploy)
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                var response = await DeployWar(appManager, files, new ZipDeployMetadata());
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot/webapps/ROOT");
            });
        }

        [Fact]
        public Task TestSimpleWarDeploymentWithCustomAppName()
        {
            return ApplicationManager.RunAsync("TestSimpleWarDeploymentPerformsCleanDeployment", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                var response = await DeployWar(appManager, files, new ZipDeployMetadata(), "testappname");
                response.EnsureSuccessStatusCode();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "site/wwwroot/webapps/testappname");
            });
        }

        private static async Task AssertSuccessfulDeploymentByContent(ApplicationManager appManager, TestFile[] files)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);
            Assert.Equal(GetZipDeployer(), deployment.Deployer);

            var entries = await appManager.VfsWebRootManager.ListAsync(null);
            var deployedFilenames = entries.Select(e => e.Name);

            var filenameSet = new HashSet<string>(files.Select(f => f.Filename));
            Assert.True(filenameSet.SetEquals(entries.Select(e => e.Name)));

            foreach (var file in files)
            {
                var deployedContent = await appManager.VfsWebRootManager.ReadAllTextAsync(file.Filename);
                Assert.Equal(file.Content, deployedContent);
            }
        }

        private static async Task AssertSuccessfulDeployment(ApplicationManager appManager, int timeoutSecs = 30)
        {
            DeployResult deployment = null;
            for (int i = 0; i < timeoutSecs; ++i)
            {
                deployment = await appManager.DeploymentManager.GetResultAsync("latest");
                if (deployment.Status == DeployStatus.Success || deployment.Status == DeployStatus.Failed)
                {
                    break;
                }

                await Task.Delay(1000);
            }

            Assert.Equal(DeployStatus.Success, deployment.Status);
        }

        private static async Task<HttpResponseMessage> DeployZip(
            ApplicationManager appManager,
            TestFile[] files,
            ZipDeployMetadata metadata)
        {
            TestTracer.Trace("Push-deploying zip");
            using (var zipStream = DeploymentTestHelper.CreateZipStream(files))
            {
                return await appManager.ZipDeploymentManager.PushDeployFromStream(
                    zipStream,
                    metadata);
            }
        }

        private static async Task<HttpResponseMessage> DeployWar(
            ApplicationManager appManager,
            TestFile[] files,
            ZipDeployMetadata metadata,
            string appName = null)
        {
            TestTracer.Trace("Push-deploying war");
            using (var zipStream = DeploymentTestHelper.CreateZipStream(files))
            {
                IList<KeyValuePair<string, string>> queryParams = null;
                if (!string.IsNullOrWhiteSpace(appName))
                {
                    queryParams = new List<KeyValuePair<string, string>>(){ new KeyValuePair<string, string>( "name", appName ) };
                }

                return await appManager.WarDeploymentManager.PushDeployFromStream(
                    zipStream,
                    metadata,
                    queryParams);
            }
        }

        private static string GetZipDeployer()
        {
            return KuduUtils.RunningAgainstLinuxKudu ? "Push-Deployer" : "ZipDeploy";
        }

        private static string simplePackageJsonWithDependencyContent = @"{
          ""name"": ""test"",
          ""version"": ""1.0.0"",
          ""dependencies"": {
            ""express"": """"
          }
        }
        ";

    }
}
