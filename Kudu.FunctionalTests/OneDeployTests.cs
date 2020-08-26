using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
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

        #region Test entry point artifacts: app.war, app.jar, app.ear

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestAppDotWarDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestAppDotWarDeployment", async appManager =>
            {
                ConfigureSiteAsTomcatSite(appManager);

                await DeployNonZippedArtifact(appManager, "war", null, isAsync);

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html", "app.war" }, "site/wwwroot");
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestAppDotJarDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestAppDotJarDeployment", async appManager =>
            {
                ConfigureSiteAsJavaSESite(appManager);

                await DeployNonZippedArtifact(appManager, "jar", null, isAsync);

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html", "app.jar" }, "site/wwwroot");
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestAppDotEarDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestAppDotEarDeployment", async appManager =>
            {
                ConfigureSiteAsJbossEapSite(appManager);

                await DeployNonZippedArtifact(appManager, "ear", null, isAsync);

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html", "app.ear" }, "site/wwwroot");
            });
        }

        #endregion

        #region Test non-entry point artifacts: type = lib, static, startup, zip

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestLibDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestLibDeployment", async appManager =>
            {
                ConfigureSiteAsJavaSESite(appManager);

                await DeployNonZippedArtifact(appManager, "lib", "site/wwwroot/dir1/dir2/library.jar", isAsync);

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "library.jar" }, "site/wwwroot/dir1/dir2");
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestStaticFileDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestStaticFileDeployment", async appManager =>
            {
                await DeployNonZippedArtifact(appManager, "static", "site/wwwroot/statictestdir/statictestfile.txt", isAsync);

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "statictestfile.txt" }, "site/wwwroot/statictestdir");
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestStartupFileDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestStartupFileDeployment", async appManager =>
            {
                var originalStartupFileContent = await DeployNonZippedArtifact(appManager, "startup", path: "invalid/path/to/be/sure/it/is/ignored/by/OneDeploy", isAsync: isAsync);

                TestTracer.Trace("Verifying startup file is deployed and deployment record created.");

                var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

                Assert.Equal(DeployStatus.Success, deployment.Status);

                var observedStartupFileContent = appManager.VfsManager.ReadAllText("startup.bat");

                Assert.Equal(originalStartupFileContent, observedStartupFileContent);
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestZipDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestZipDeployment", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);
                await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "zip", null, isAsync);

                var expectedFiles = files.Select(f => f.Filename).ToList();
                expectedFiles.Add("hostingstart.html");
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles.ToArray(), "site/wwwroot");
            });
        }

        #endregion

        #region Advanced scenarios

        // Tests deployments to /home/site/wwwroot/webapps/<dir>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestLegacyWarDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestLegacyWarDeployment", async appManager =>
            {
                ConfigureSiteAsTomcatSite(appManager);

                // STEP 1A: First legacy war deployment
                var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "war", "site/wwwroot/webapps/ROOT", isAsync);

                // STEP 1B: Validate the result of the first legacy war deployment
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files1.Select(f => f.Filename).ToArray(), "site/wwwroot/webapps/ROOT");


                // STEP 2A: Second legacy war deployment
                var files2 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                await DeployZippedArtifact(appManager, appManager.OneDeployManager, files2, "war", "site/wwwroot/webapps/ROOT", isAsync);

                // STEP 2B: Validate that the second legacy war deployment cleans up the files deployes by the first deployment
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, files2.Select(f => f.Filename).ToArray(), "site/wwwroot/webapps/ROOT");
            });
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public Task TestURLBasedDeployment(bool isAsync, bool isArmRequest)
        {
            return ApplicationManager.RunAsync("TestURLDeployment", async appManager =>
            {
                ConfigureSiteAsTomcatSite(appManager);

                var client = appManager.OneDeployManager.Client;
                var requestUri = isArmRequest ? $"{client.BaseAddress}" : $"{client.BaseAddress}?type=static&restart=false&path=site/wwwroot/NOTICE.txt&async={isAsync}";
                using (var request = new HttpRequestMessage(HttpMethod.Put, requestUri))
                {
                    var packageUri = "https://github.com/projectkudu/kudu/blob/master/NOTICE.txt?raw=true";

                    if (isArmRequest)
                    {
                        var payload = new
                        {
                            properties = new
                            {
                                packageUri = packageUri,
                                type = "static",
                                restart = false,
                                path = "site/wwwroot/NOTICE.txt",
                                async = isAsync,
                            }
                        };

                        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                        request.Headers.Referrer = new Uri("https://management.azure.com/subscriptions/sub-id/resourcegroups/rg-name/providers/Microsoft.Web/sites/site-name/extensions/publish?api-version=2016-03-01");
                        request.Headers.Add("x-ms-geo-location", "westus");
                    }
                    else
                    {
                        var payload = new { packageUri = packageUri };
                        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    }

                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    if (isAsync || isArmRequest)
                    {
                        await DeploymentTestHelper.WaitForDeploymentCompletionAsync(appManager, deployer);
                    }

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html", "NOTICE.txt" }, "site/wwwroot");
                }
            });
        }

        //
        // Creates a war and static file to deploy one after the other and checks for both files in /wwwroot
        //
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestIncrementalDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestIterativeDeployment", async appManager =>
            {
                await DeployNonZippedArtifact(appManager, "static", "site/wwwroot/staticfiletest1.txt", isAsync);
                await DeployNonZippedArtifact(appManager, "static", "site/wwwroot/staticfiletest2.txt", isAsync);

                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html", "staticfiletest1.txt", "staticfiletest2.txt" }, "site/wwwroot");
            });
        }

        // Tests that OneDeploy propagates the ZipDeploy manifest correct
        // to ensure that manifest-based deployments (eg: zipdeploy) are not affected by
        // OneDeploy based deployments
        [Fact]
        public Task TestManifestPropagatedCorrectly()
        {
            return ApplicationManager.RunAsync("TestManifestPropagatedCorrectly", async appManager =>
            {
                // We run this test in 3 steps: Zipdeploy, then OneDeploy, and then ZipDeploy again
                // And then we check if the second ZipDeploy works as expected

                // STEP 1a: Upload a zip using ZipDeploy
                var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                await DeployZippedArtifact(appManager, appManager.ZipDeploymentManager, files1, type: "", path: "", isAsync: false);

                // STEP 1b: Validate the ZipDeploy result
                var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");

                // STEP 2a: Upload a text file using OneDeploy
                await DeployNonZippedArtifact(appManager, "static", "site/wwwroot/staticfiletest.txt", isAsync: false);

                // STEP 2b: Validate the OneDeploy result
                expectedFiles1.Add("staticfiletest.txt");
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");

                // STEP 3a: Upload a zip using ZipDeploy
                var files2 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                await DeployZippedArtifact(appManager, appManager.ZipDeploymentManager, files2, type: "", path: "", isAsync: false);

                // STEP 3b: Validate the ZipDeploy result
                // Specifically, we want to make sure that the files deployed by the first 
                // ZipDeploy are wiped out but the file deployed by OneDeploy is left untouched
                // If this happens, we can assume that OneDeploy does not interfere with ZipDeploy
                var expectedFiles2 = files2.Select(f => f.Filename).ToList();
                expectedFiles2.Add("staticfiletest.txt");
                await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles2.ToArray(), "site/wwwroot");
            });
        }

        #endregion

        #region Helper methods

        private static async Task<string> DeployNonZippedArtifact(
            ApplicationManager appManager,
            string type,
            string path,
            bool isAsync)
        {
            TestTracer.Trace("Deploying file");

            var testFile = DeploymentTestHelper.CreateRandomTestFile();
            using (var fileStream = DeploymentTestHelper.CreateFileStream(testFile))
            {
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, isAsync);

                var response = await appManager.OneDeployManager.PushDeployFromStream(fileStream, new ZipDeployMetadata(), queryParams);
                response.EnsureSuccessStatusCode();

                if (isAsync)
                {
                    await DeploymentTestHelper.WaitForDeploymentCompletionAsync(appManager, deployer);
                }
            }

            return testFile.Content;
        }

        private static async Task DeployZippedArtifact(ApplicationManager applicationManager,
                                                                            RemotePushDeploymentManager deploymentManager,
                                                                            TestFile[] files,
                                                                            string type,
                                                                            string path,
                                                                            bool isAsync)
        {
            TestTracer.Trace("Deploying zip");
            using (var zipStream = DeploymentTestHelper.CreateZipStream(files))
            {
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, isAsync);

                var response = await deploymentManager.PushDeployFromStream(zipStream, new ZipDeployMetadata(), queryParams);
                response.EnsureSuccessStatusCode();

                if (isAsync)
                {
                    await DeploymentTestHelper.WaitForDeploymentCompletionAsync(applicationManager, deployer);
                }
            }
        }

        private static IList<KeyValuePair<string, string>> GetOneDeployQueryParams(string type, string path, bool isAsync)
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

            if (isAsync != false)
            {
                queryParams.Add(new KeyValuePair<string, string>("async", $"{isAsync}"));
            }

            //
            // Restarting using /api/restart fails because of cert check failure
            // unless running in Azure.
            // OneDeploy uses /api/restart, so we disable restart in the tests.
            //
            queryParams.Add(new KeyValuePair<string, string>("restart", "false"));

            return queryParams;
        }

        private void ConfigureSiteAsTomcatSite(ApplicationManager appManager)
        {
            // Obfuscate case to check for case-insensitivity
            appManager.SettingsManager.SetValue("WEBSITE_STACK", "TOMcat").Wait();
        }

        private void ConfigureSiteAsJavaSESite(ApplicationManager appManager)
        {
            // Obfuscate case to check for case-insensitivity
            appManager.SettingsManager.SetValue("WEBSITE_STACK", "JAva").Wait();
        }

        private void ConfigureSiteAsJbossEapSite(ApplicationManager appManager)
        {
            // Obfuscate case to check for case-insensitivity
            appManager.SettingsManager.SetValue("WEBSITE_STACK", "JBOSSeap").Wait();
        }

        #endregion
    }
}