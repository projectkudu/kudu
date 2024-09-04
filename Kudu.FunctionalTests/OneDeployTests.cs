using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
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
                    await DeployNonZippedArtifact(appManager, "war", "test.war", isAsync, null, "site/wwwroot/test.war");
                    await DeployNonZippedArtifact(appManager, "war", "/home/site/wwwroot/nonroot.war", isAsync, null, "site/wwwroot/nonroot.war");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "webapps", "hostingstart.html", "app.war", "test.war", "nonroot.war"}, "site/wwwroot");
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
                    // Relative path to wwwroot
                    await DeployNonZippedArtifact(appManager, "static", "a.txt", isAsync, false, "site/wwwroot/a.txt");
                    await DeployNonZippedArtifact(appManager, "static", "dir1/dir1a.txt", isAsync, false, "site/wwwroot/dir1/dir1a.txt");

                    // Absolute path to wwwroot
                    await DeployNonZippedArtifact(appManager, "static", "/home/site/wwwroot/b.txt", isAsync, false, "site/wwwroot/b.txt");
                    await DeployNonZippedArtifact(appManager, "static", "/home/site/wwwroot/dir1/dir1b.txt", isAsync, false, "site/wwwroot/dir1/dir1b.txt");

                    // Absolute path to non-wwwroot
                    await DeployNonZippedArtifact(appManager, "static", "/home/testdir/dir1/dir1a.txt", isAsync, false, "testdir/dir1/dir1a.txt");
                    await DeployNonZippedArtifact(appManager, "static", "/home/testdir/dir1/dir1b.txt", isAsync, false, "testdir/dir1/dir1b.txt");

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { initialFileName, "webapps", "a.txt", "b.txt", "dir1", "hostingstart.html" }, "site/wwwroot");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir1a.txt", "dir1b.txt" }, "site/wwwroot/dir1");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir1a.txt", "dir1b.txt" }, "testdir/dir1");
                }

                // Clean deployment
                {
                    // Relative path to wwwroot
                    await DeployNonZippedArtifact(appManager, "static", "dir1/dir1c.txt", isAsync, true, "site/wwwroot/dir1/dir1c.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir1c.txt" }, "site/wwwroot/dir1");

                    // Absolute path to wwwroot
                    await DeployNonZippedArtifact(appManager, "static", "/home/site/wwwroot/c.txt", isAsync, true, "site/wwwroot/c.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "c.txt", }, "site/wwwroot");

                    // Absolute path to non-wwwroot
                    await DeployNonZippedArtifact(appManager, "static", "/home/testdir/dir1/dir1c.txt", isAsync, true, "testdir/dir1/dir1c.txt");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "dir1c.txt" }, "testdir/dir1");
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
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", null, isAsync, isClean:true);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");
                }

                // Custom path - clean
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "dir1/dir2", isAsync, isClean: true);
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string [] { "dir1" }, "site/wwwroot");
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot/dir1/dir2");
                }
            });
        }

        #endregion

        #region Path specific tests

        // Tests that invalid paths return 400 (Bad Request) and not 5xx.
        [Fact]
        public Task TestInvalidPaths()
        {
            return ApplicationManager.RunAsync("TestInvalidPaths", async appManager =>
            {
                var files = DeploymentTestHelper.CreateRandomFilesForZip(10);

                //
                // NOTE: For each type, run a successful scenario to validate that all is good and then
                // begin with the tests for invalid paths where we expect the test to fail
                //

                // Legacy war - relative paths
                {
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "webapps/ROOT", isAsync: false, isClean: false, expectedSuccess: true);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "webapps/ROOT/", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "invalid", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "webapps/app/foo", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "webapps2/app", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "webapps", isAsync: false, isClean: false, expectedSuccess: false);
                }

                // Legacy war - absolute paths
                {
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/webapps/app", isAsync: false, isClean: false, expectedSuccess: true);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/webapps/app/", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/webapps/app/foo", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/webapps/", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/webapps", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/webapps2/app", isAsync: false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files, "war", "/home/site/wwwroot/", isAsync: false, isClean: false, expectedSuccess: false);
                }

                // Type zip
                {
                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(2);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/..", false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/../..", false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "../test", false, isClean: false, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home", false, isClean: true, expectedSuccess: false);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site", false, isClean: true, expectedSuccess: false);
                }

                // Type static
                {
                    await DeployNonZippedArtifact(appManager, "static", "../test.txt", false, null, expectedSuccess: false);
                    await DeployNonZippedArtifact(appManager, "static", "../../test.txt", false, null, expectedSuccess: false);
                    await DeployNonZippedArtifact(appManager, "static", "/home/test.txt", false, true, expectedSuccess: false);
                    await DeployNonZippedArtifact(appManager, "static", "/home/site/test.txt", false, true, expectedSuccess: false);
                }
            });
        }

        #endregion


        #region Advanced scenarios

        //Tests type zip deployments outside wwwroot w/ absolute paths
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestZipDeploymentOutsideWWWroot(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestZipDeploymentOutsideWWWroot", async appManager =>
            {
                // Incremental deployment to /site/test
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/test", isAsync, isClean: false);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    expectedFiles1.Add(initialFileName);
                    expectedFiles1.Add("test2");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/test");
                }

                // Clean deployment to site/test 
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/test", isAsync, isClean: true);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/test");
                }

                // Incremental Deployment to site/test/test2
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/test/test2", isAsync, isClean: false);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    expectedFiles1.Add(initialFileName);
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/test/test2");
                }

                // Clean deployment to site/test/test2
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/test/test2/", isAsync, isClean: true);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/test/test2");
                }
            });
        }

        //Tests type zip deployments inside wwwroot to absolute paths
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestZipDeploymentAbsPath(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestZipDeploymentAbsPath", async appManager =>
            {
                // Incremental deployment wwwroot w/ absolute path
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/wwwroot/", isAsync, isClean: false);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    expectedFiles1.Add(initialFileName);
                    expectedFiles1.Add("webapps");
                    expectedFiles1.Add("hostingstart.html");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");
                }

                // Clean deployment to wwwroot w/ absolute path
                {
                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/wwwroot/", isAsync, isClean: true);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot");
                }

                // Incremental deployment wwwroot/test w/ absolute path
                {
                    var initialFileName = DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/wwwroot/webapps", isAsync, isClean: false);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    expectedFiles1.Add(initialFileName);
                    expectedFiles1.Add("ROOT");
                    expectedFiles1.Add("ROOT2");
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot/webapps");
                }

                // Clean deployment to wwwroot/test w/ absolute path
                {
                    var files1 = DeploymentTestHelper.CreateRandomFilesForZip(10);
                    await DeployZippedArtifact(appManager, appManager.OneDeployManager, files1, "zip", "/home/site/wwwroot/webapps", isAsync, isClean: true);
                    var expectedFiles1 = files1.Select(f => f.Filename).ToList();
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, expectedFiles1.ToArray(), "site/wwwroot/webapps");
                }
            });
        }

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
                var requestUri = isArmRequest ? $"{client.BaseAddress}" : $"{client.BaseAddress}?type=static&restart=false&path=/home/site/wwwroot/NOTICE.txt&async={isAsync}";
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
                                path = "/home/site/wwwroot/NOTICE.txt",
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
                    TestTracer.Trace($"Response code={response.StatusCode}");
                    response.EnsureSuccessStatusCode();

                    if (isAsync || isArmRequest)
                    {
                        await DeploymentTestHelper.WaitForDeploymentCompletionAsync(appManager, deployer);
                    }

                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html", "NOTICE.txt" }, "site/wwwroot");
                }
                TestTracer.Trace($"Validation successful!");
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
                await DeployNonZippedArtifact(appManager, "static", "/home/site/wwwroot/staticfiletest.txt", isAsync: false, isClean: null);

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task TestResetToDefault(bool isAsync)
        {
            return ApplicationManager.RunAsync("TestResetToDefault", async appManager =>
            {
                // Default deployment mode - overwrite app.jar
                {
                    DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);
                    await DeployEmptyArtifact(appManager, null, isAsync, true, true);
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] {"hostingstart.html"}, "site/wwwroot");
                }

                DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);

                // Clean deployment
                {
                    DeploymentTestHelper.DeployRandomFilesEverywhere(appManager);
                    await DeployEmptyArtifact(appManager, null, isAsync, true, true);
                    await DeploymentTestHelper.AssertSuccessfulDeploymentByFilenames(appManager, new string[] { "hostingstart.html" }, "site/wwwroot");
                }
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
            string expectedDeployPath = null,
            bool expectedSuccess = true)
        {
            TestTracer.Trace($"Deploying file type={type} path={path} isAsync={isAsync} isClean={isClean} expectedDeployPath={expectedDeployPath} expectedSuccess={expectedSuccess}");

            var testFile = DeploymentTestHelper.CreateRandomTestFile();
            using (var fileStream = DeploymentTestHelper.CreateFileStream(testFile))
            {
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, isAsync, isClean);

                var response = await appManager.OneDeployManager.PushDeployFromStream(fileStream, new ZipDeployMetadata(), queryParams);
                TestTracer.Trace($"Response code={response.StatusCode}");
                if (expectedSuccess)
                {
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    Assert.True(response.StatusCode == System.Net.HttpStatusCode.BadRequest, $"This test is expected to fail with status code == 400. Observed status code == {response.StatusCode}");
                }

                if (isAsync)
                {
                    await DeploymentTestHelper.WaitForDeploymentCompletionAsync(appManager, deployer);
                }
            }

            if (expectedDeployPath != null)
            {
                VerifyDeployedArtifact(appManager, testFile.Content, expectedDeployPath);
            }

            TestTracer.Trace($"Validation successful!");
            return testFile.Content;
        }

        private static async Task DeployZippedArtifact(ApplicationManager applicationManager,
                                                                            RemotePushDeploymentManager deploymentManager,
                                                                            TestFile[] files,
                                                                            string type,
                                                                            string path,
                                                                            bool isAsync,
                                                                            bool? isClean = null,
                                                                            bool expectedSuccess = true)
        {
            TestTracer.Trace($"Deploying zip type={type} path={path} isAsync={isAsync} isClean={isClean} expectedSuccess={expectedSuccess}");
            using (var zipStream = DeploymentTestHelper.CreateZipStream(files))
            {
                IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, path, isAsync, isClean);

                var response = await deploymentManager.PushDeployFromStream(zipStream, new ZipDeployMetadata(), queryParams);
                TestTracer.Trace($"Response code={response.StatusCode}");
                if (expectedSuccess)
                {
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    Assert.True(response.StatusCode == System.Net.HttpStatusCode.BadRequest, $"This test is expected to fail with status code == 400. Observed status code == {response.StatusCode}");
                }

                if (isAsync)
                {
                    await DeploymentTestHelper.WaitForDeploymentCompletionAsync(applicationManager, deployer);
                }
            }
            TestTracer.Trace($"Validation successful!");
        }

        private static async Task<string> DeployEmptyArtifact(
            ApplicationManager appManager,
            string type,
            bool isAsync,
            bool reset,
            bool expectedSuccess)
        {
            TestTracer.Trace($"Deploying file type={type} isAsync={isAsync}");

            IList<KeyValuePair<string, string>> queryParams = GetOneDeployQueryParams(type, null, isAsync, null, reset);

            var response = await appManager.OneDeployManager.PushDeployFromStream(null, new ZipDeployMetadata(), queryParams);
            TestTracer.Trace($"Response code={response.StatusCode}");
            if (expectedSuccess)
            {
                response.EnsureSuccessStatusCode();
            }
            else
            {
                Assert.True(response.StatusCode == System.Net.HttpStatusCode.BadRequest, $"This test is expected to fail with status code == 400. Observed status code == {response.StatusCode}");
            }

            if (isAsync)
            {
                await DeploymentTestHelper.WaitForDeploymentCompletionAsync(appManager, deployer);
            }

            TestTracer.Trace($"Validation successful!");
            return "reset";
        }

        private static IList<KeyValuePair<string, string>> GetOneDeployQueryParams(string type, string path, bool isAsync, bool? isClean, bool reset = false)
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

            if (reset != false)
            {
                queryParams.Add(new KeyValuePair<string, string>("reset", $"{reset}"));
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