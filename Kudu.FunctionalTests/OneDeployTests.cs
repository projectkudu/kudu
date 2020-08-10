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
using static Kudu.FunctionalTests.DeploymentTestHelper;

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
                    await WaitForDeploymentCompletionAsync(appManager, deployer);
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
                    await WaitForDeploymentCompletionAsync(appManager, deployer);
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
                    await WaitForDeploymentCompletionAsync(appManager, deployer);
                }

                await AssertSuccessfulDeploymentByFilenames(appManager, new string[] { fileName }, "staticfiles");
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
                    await WaitForDeploymentCompletionAsync(appManager, deployer);
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
                    await WaitForDeploymentCompletionAsync(appManager, deployer);
                }

                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());
            });
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public Task TestURLBasedDeployment(string async)
        {
            var packageUri = " https://github.com/projectkudu/kudu/blob/master/Kudu.FunctionalTests/green.war?raw=true";
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
                            await WaitForDeploymentCompletionAsync(appManager, deployer);
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
        //Creates a war and static file to deploy one after the other and checks for both files in /wwwroot
        public Task TestIncrementalDeployment()
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
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, async);
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
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, async);

                return await appManager.OneDeployManager.PushDeployFromStream(
                    zipStream,
                    metadata,
                    queryParams);
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

        private static IList<KeyValuePair<string, string>> GetOneDeployQueryParams(string type, string path, string async)
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
            return queryParams;
        }
    }
}