using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
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
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Default deployment mode - overwrite app.war
                {
                    await DeployNonZippedArtifact(appManager, "war", null, isAsync, null, "site/wwwroot/app.war");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "webapps", "hostingstart.html", "app.war" }, "site/wwwroot");
                }

                // Clean deployment
                {
                    await DeployNonZippedArtifact(appManager, "war", null, isAsync, true, "site/wwwroot/app.war");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.war" }, "site/wwwroot");
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestAppDotJarDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestAppDotJarDeployment", async appManager =>
            {
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Default deployment mode - overwrite app.jar
                {
                    await DeployNonZippedArtifact(appManager, "jar", null, isAsync, null, "site/wwwroot/app.jar");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "webapps", "hostingstart.html", "app.jar" }, "site/wwwroot");
                }

                // Clean deployment
                {
                    await DeployNonZippedArtifact(appManager, "jar", null, isAsync, true, "site/wwwroot/app.jar");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.jar" }, "site/wwwroot");
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestAppDotEarDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestAppDotEarDeployment", async appManager =>
            {
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Default deployment mode - overwrite app.ear
                {
                    await DeployNonZippedArtifact(appManager, "ear", null, isAsync, null, "site/wwwroot/app.ear");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "webapps", "hostingstart.html", "app.ear" }, "site/wwwroot");
                }

                // Clean Deployment
                {
                    await DeployNonZippedArtifact(appManager, "ear", null, isAsync, true, "site/wwwroot/app.ear");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "app.ear" }, "site/wwwroot");
                }
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
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Default deployment - incremental
                {
                    await DeployNonZippedArtifact(appManager, "lib", "library1.jar", isAsync, null, "site/libs/library1.jar");
                    await DeployNonZippedArtifact(appManager, "lib", "library2.jar", isAsync, null, "site/libs/library2.jar");
                    await DeployNonZippedArtifact(appManager, "lib", "dir1/dir2/library3.jar", isAsync, null, "site/libs/dir1/dir2/library3.jar");
                    await DeployNonZippedArtifact(appManager, "lib", "dir1/dir2/library4.jar", isAsync, null, "site/libs/dir1/dir2/library4.jar");

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "library1.jar", "library2.jar", "dir1" }, "site/libs");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "library3.jar", "library4.jar" }, "site/libs/dir1/dir2");
                }

                // Clean deployment
                {
                    await DeployNonZippedArtifact(appManager, "lib", "dir/library.jar", isAsync, true, "site/libs/dir/library.jar");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir" }, "site/libs");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "library.jar" }, "site/libs/dir");

                    await DeployNonZippedArtifact(appManager, "lib", "library.jar", isAsync, true, "site/libs/library.jar");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "library.jar" }, "site/libs");
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestScriptDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestScriptDeployment", async appManager =>
            {
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Default deployment - incremental
                {
                    await DeployNonZippedArtifact(appManager, "script", "script1.txt", isAsync, null, "site/scripts/script1.txt");
                    await DeployNonZippedArtifact(appManager, "script", "dir1/script2.txt", isAsync, null, "site/scripts/dir1/script2.txt");

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "dir1", "script1.txt" }, "site/scripts");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "script2.txt" }, "site/scripts/dir1");
                }

                // Clean deployment
                {
                    await DeployNonZippedArtifact(appManager, "script", "dir/script2.txt", isAsync, true, "site/scripts/dir/script2.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir" }, "site/scripts");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "script2.txt" }, "site/scripts/dir");

                    await DeployNonZippedArtifact(appManager, "script", "script2.txt", isAsync, true, "site/scripts/script2.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "script2.txt" }, "site/scripts");
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestStaticFileDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestStaticFileDeployment", async appManager =>
            {
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Default deployment - incremental
                {
                    await DeployNonZippedArtifact(appManager, "static", "a.txt", isAsync, false, "site/wwwroot/a.txt");
                    await DeployNonZippedArtifact(appManager, "static", "b.txt", isAsync, false, "site/wwwroot/b.txt");
                    await DeployNonZippedArtifact(appManager, "static", "dir1/dir1a.txt", isAsync, false, "site/wwwroot/dir1/dir1a.txt");
                    await DeployNonZippedArtifact(appManager, "static", "dir1/dir1b.txt", isAsync, false, "site/wwwroot/dir1/dir1b.txt");

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "webapps", "a.txt", "b.txt", "dir1", "hostingstart.html" }, "site/wwwroot");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir1a.txt", "dir1b.txt" }, "site/wwwroot/dir1");
                }

                // Clean deployment
                {
                    await DeployNonZippedArtifact(appManager, "static", "dir2/dir2a.txt", isAsync, true, "site/wwwroot/dir2/dir2a.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir2" }, "site/wwwroot");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir2a.txt" }, "site/wwwroot/dir2");

                    await DeployNonZippedArtifact(appManager, "static", "c.txt", isAsync, true, "site/wwwroot/c.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "c.txt",  }, "site/wwwroot");
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestStartupFileDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestStartupFileDeployment", async appManager =>
            {
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);
                var startupFileName = KuduUtils.RunningAgainstLinuxKudu ? "startup.sh" : "startup.cmd";

                // Default deployment mode - overwrite previous startup file
                {
                    await DeployNonZippedArtifact(appManager, "startup", "invalid/path/to/test/that/it/is/ignored", isAsync, true, $"site/scripts/{startupFileName}");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { startupFileName }, "site/scripts");
                }

                // Clean deployment
                {
                    await DeployNonZippedArtifact(appManager, "startup", "invalid/path/to/test/that/it/is/ignored", isAsync, true, $"site/scripts/{startupFileName}");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { startupFileName }, "site/scripts");
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestZipDeployment(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestZipDeployment", async appManager =>
            {
                // Incremental deployment - not the default mode
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", null, isAsync, isClean: false);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    expectedFiles1.Add(initialFileName);
                    expectedFiles1.Add("webapps");
                    expectedFiles1.Add("hostingstart.html");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");
                }

                // Default deployment - clean
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", null, isAsync);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");
                }

                // Custom path
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "dir1/dir2", isAsync);
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string [] { "dir1" }, "site/wwwroot");
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot/dir1/dir2");
                }
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
                var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Incremental deployment - NOT supported, will be ignored
                {
                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "war", "webapps/ROOT", isAsync, isClean: false);

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "ROOT", "ROOT2" }, "site/wwwroot/webapps");
                    var expectedFiles1 = files1.Select(file => file.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot/webapps/ROOT");
                }
                
                // Default deployment - clean
                {
                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "war", "webapps/ROOT2", isAsync);

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "ROOT", "ROOT2" }, "site/wwwroot/webapps");
                    var expectedFiles1 = files1.Select(file => file.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot/webapps/ROOT2");
                }
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
                var client = appManager.OneDeployManager.Client;
                var requestUri = isArmRequest ? $"{client.BaseAddress}" : $"{client.BaseAddress}?type=static&restart=false&path=NOTICE.txt&async={isAsync}";
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
                                path = "NOTICE.txt",
                                async = isAsync,
                                ignorestack = "true",
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
                await DeployNonZippedArtifact(appManager, "static", "staticfiletest.txt", isAsync: false, isClean: null);

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

        private static void VerifyDeployedArtifact(ApplicationManager appManager, string originalFileContent, string filePath)
        {
            var observerdFileContent = appManager.VfsManager.ReadAllText(filePath);
            Assert.Equal(originalFileContent, observerdFileContent);
        }

        private static async Task<string> DeployNonZippedArtifact(
            ApplicationManager appManager,
            string type,
            string path,
            bool isAsync,
            bool? isClean,
            string expectedDeployPath = null)
        {
            TestTracer.Trace("Deploying file");

            var testFile = DeploymentTestHelper.CreateRandomTestFile();
            using (var fileStream = DeploymentTestHelper.CreateFileStream(testFile))
            {
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, isAsync, isClean);

                var response = await appManager.OneDeployManager.PushDeployFromStream(fileStream, new ZipDeployMetadata(), queryParams);
                response.EnsureSuccessStatusCode();

                if (isAsync)
                {
                    await DeploymentTestHelper.WaitForDeploymentCompletionAsync(appManager, deployer);
                }
            }

            if (expectedDeployPath != null)
            {
                VerifyDeployedArtifact(appManager, testFile.Content, expectedDeployPath);
            }

            return testFile.Content;
        }

        private static async Task DeployZippedArtifact(ApplicationManager applicationManager,
                                                                            RemotePushDeploymentManager deploymentManager,
                                                                            TestFile[] files,
                                                                            string type,
                                                                            string path,
                                                                            bool isAsync,
                                                                            bool? isClean = null)
        {
            TestTracer.Trace("Deploying zip");
            using (var zipStream = DeploymentTestHelper.CreateZipStream(files))
            {
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, isAsync, isClean);

                var response = await deploymentManager.PushDeployFromStream(zipStream, new ZipDeployMetadata(), queryParams);
                response.EnsureSuccessStatusCode();

                if (isAsync)
                {
                    await DeploymentTestHelper.WaitForDeploymentCompletionAsync(applicationManager, deployer);
                }
            }
        }

        private static IList<KeyValuePair<string, string>> GetOneDeployQueryParams(string type, string path, bool isAsync, bool? isClean)
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

            if (isClean.HasValue)
            {
                queryParams.Add(new KeyValuePair<string, string>("clean", $"{isClean}"));
            }

            queryParams.Add(new KeyValuePair<string, string>("ignorestack", "true"));

            //
            // Restarting using /api/restart fails because of cert check failure
            // unless running in Azure.
            // OneDeploy uses /api/restart, so we disable restart in the tests.
            //
            queryParams.Add(new KeyValuePair<string, string>("restart", "false"));

            return queryParams;
        }

        #endregion
    }
}