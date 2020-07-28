using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web.SessionState;
using System.Web.UI.WebControls;
using Kudu.Client.Deployment;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class OneDeployTests
    {
        public const string deployer = "OneDeploy";


        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestAppDotWarDeployment(string async)
        {
            return ApplicationManager.RunAsync("TestWarDeployment", async appManager =>
            {
                var file = CreateRandomTestFile();
                var response = await DeployNonZippedArtifact(appManager, file, new ZipDeployMetadata(), "war", null, async);
                response.EnsureSuccessStatusCode();

                if (async == "true")
                {
                    DeployResult result;
                    do
                    {
                        result = await appManager.DeploymentManager.GetResultAsync("latest");
                        Assert.Equal(deployer, result.Deployer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
                }
                
                await AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.war" });
            });
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestLegacyWarDeployment(string async)
        {
            return ApplicationManager.RunAsync("TestLegacyWarDeployment", async appManager =>
            {
                var files = CreateRandomFilesForZip(10);
                var response = await DeployZippedArtifact(appManager, files, new ZipDeployMetadata(), "war", "webapps/ROOT", async);
                response.EnsureSuccessStatusCode();

                if (async == "true")
                {
                    DeployResult result;
                    do
                    {
                        result = await appManager.DeploymentManager.GetResultAsync("latest");
                        Assert.Equal(deployer, result.Deployer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
                }

                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray(), "webapps/ROOT");
            });
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestStaticDeployment(string async)
        {
            return ApplicationManager.RunAsync("TestStaticDeployment", async appManager =>
            {
                var file = CreateRandomTestFile();
                var fileName = Path.GetRandomFileName();
                var response = await DeployNonZippedArtifact(appManager, file, new ZipDeployMetadata(), "static", Path.Combine("text", fileName), async);
                response.EnsureSuccessStatusCode();

                if (async == "true")
                {
                    DeployResult result;
                    do
                    {
                        result = await appManager.DeploymentManager.GetResultAsync("latest");
                        Assert.Equal(deployer, result.Deployer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
                }

                await AssertSuccessfulDeploymentByFilenames(appManager, new string[] { fileName }, "text");
            });
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestAppDotJarDeployment(string async)
        {
            return ApplicationManager.RunAsync("TestJarDeployment", async appManager =>
            {
                var file = CreateRandomTestFile();
                var response = await DeployNonZippedArtifact(appManager, file, new ZipDeployMetadata(), "jar", null, async);
                response.EnsureSuccessStatusCode();

                if (async == "true")
                {
                    DeployResult result;
                    do
                    {
                        result = await appManager.DeploymentManager.GetResultAsync("latest");
                        Assert.Equal(deployer, result.Deployer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
                }

                await AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.jar" });
            });
        }


        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestZipDeployment(string async)
        {
            return ApplicationManager.RunAsync("TestZipDeployment", async appManager =>
            {
                var files = CreateRandomFilesForZip(10);
                var response = await DeployZippedArtifact(appManager, files, new ZipDeployMetadata(), "zip", null, async);
                response.EnsureSuccessStatusCode();

                if (async == "true")
                {
                    DeployResult result;
                    do
                    {
                        result = await appManager.DeploymentManager.GetResultAsync("latest");
                        Assert.Equal(deployer, result.Deployer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
                }

                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());
            });
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestURLBasedDeployment(string async)
        {
            var packageUri = "https://github.com/dannysongg/testrepo/blob/master/green.war?raw=true";
            return ApplicationManager.RunAsync("TestURLDeployment", async appManager =>
            {
                var client = appManager.OneDeployManager.Client;
                var requestUri = client.BaseAddress + "?type=war&async=" + async;
                using (var request = new HttpRequestMessage(HttpMethod.Put, requestUri))
                {
                    var payload = new { packageUri = packageUri };
                    request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                    using (var response = await client.SendAsync(request))
                    {
                        if (async == "true")
                        {
                            DeployResult result;
                            do
                            {
                                result = await appManager.DeploymentManager.GetResultAsync("latest");
                                Assert.Equal(deployer, result.Deployer);
                                await Task.Delay(TimeSpan.FromSeconds(2));
                            } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
                        }
                        else
                        {
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        }
                        await AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.war" });
                    }
                }

            });
        }

        [Fact]
        public Task EnsureIterativeDeployment()
        {
            return ApplicationManager.RunAsync("TestIterativeDeployment", async appManager =>
            {
                var file1 = CreateRandomTestFile();
                var response1 = await DeployNonZippedArtifact(appManager, file1, new ZipDeployMetadata(), "war", null);
                response1.EnsureSuccessStatusCode();

                var file2 = CreateRandomTestFile();
                var response2 = await DeployNonZippedArtifact(appManager, file2, new ZipDeployMetadata(), "static", "iterative.txt");
                response2.EnsureSuccessStatusCode();

                await AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.war", "iterative.txt" });
            });
        }



        private static async Task<HttpResponseMessage> DeployNonZippedArtifact(
            ApplicationManager appManager,
            TestFile file,
            ZipDeployMetadata metadata,
            string type = null,
            string path = null,
            string async = "false")
        {
            TestTracer.Trace("Deploying file");
            using (var fileStream = CreateFileStream(file))
            {
                IList<KeyValuePair<string, string>> queryParams = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrWhiteSpace(type))
                {
                    queryParams.Add(new KeyValuePair<string, string>("type", type));
                }
                if (!string.IsNullOrWhiteSpace(path))
                {
                    queryParams.Add(new KeyValuePair<string, string>("path", path));
                }
                if (async != "false")
                {
                    queryParams.Add(new KeyValuePair<string, string>("async", async));
                }
                return await appManager.OneDeployManager.PushDeployFromStream(fileStream, metadata, queryParams);
            }
        }

        private static async Task<HttpResponseMessage> DeployZippedArtifact(ApplicationManager appManager,
            TestFile[] files,
            ZipDeployMetadata metadata,
            string type = null,
            string path = null,
            string async = "false")
        {
            TestTracer.Trace("Deploying zip");
            using (var zipStream = CreateZipStream(files))
            {
                IList<KeyValuePair<string, string>> queryParams = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrWhiteSpace(type))
                {
                    queryParams.Add(new KeyValuePair<string, string>("type", type));
                }
                if (!string.IsNullOrWhiteSpace(path))
                {
                    queryParams.Add(new KeyValuePair<string, string>("path", path));
                }
                if (async != "false")
                {
                    queryParams.Add(new KeyValuePair<string, string>("async", async));
                }

                return await appManager.OneDeployManager.PushDeployFromStream(
                    zipStream,
                    metadata,
                    queryParams);
            }
        }

        private static TestFile CreateRandomTestFile()
        {
            var fileValues = Guid.NewGuid().ToString("N");
            return new TestFile { Content = fileValues, Filename = fileValues }; 
        }

        private static TestFile[] CreateRandomFilesForZip(int numFiles)
        {
            return Enumerable.Range(0, numFiles)
                .Select(i => Guid.NewGuid().ToString("N"))
                .Select(s => new TestFile { Content = s, Filename = s })
                .ToArray();
        }


        private static Stream CreateZipStream(TestFile[] files)
        {
            var s = new MemoryStream();
            using (var archive = new ZipArchive(s, ZipArchiveMode.Update, true))
            {
                foreach (var file in files)
                {
                    var entry = archive.CreateEntry(file.Filename);

                    using (var w = new StreamWriter(entry.Open()))
                    {
                        w.Write(file.Content);
                    }

                    if (file.ModifiedTime.HasValue)
                    {
                        entry.LastWriteTime = file.ModifiedTime.Value;
                    }
                }
            }

            s.Position = 0;
            return s;
        }

        private static Stream CreateFileStream(TestFile file)
        {
            var s = new MemoryStream();
            using (var sw = new StreamWriter(s, Encoding.Default, file.Content.Length, true))
            {
                sw.Write(file.Content);
            }
            s.Position = 0;
            return s;
        }

        private static async Task AssertSuccessfulDeploymentByContent(ApplicationManager appManager, TestFile[] files)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);
            Assert.Equal(deployer, deployment.Deployer);

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

        private static async Task AssertSuccessfulDeploymentByFilenames(ApplicationManager appManager, string[] filenames, string path = null)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);

            var entries = await appManager.VfsWebRootManager.ListAsync(path);
            var deployedFilenames = entries.Select(e => e.Name).ToArray();

            Assert.True(!filenames.Except(deployedFilenames).Any());
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

        private class TestFile
        {
            public string Filename;
            public string Content;
            public DateTime? ModifiedTime;
        }
    }
}