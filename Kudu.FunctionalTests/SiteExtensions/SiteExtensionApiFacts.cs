using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.SiteExtensions;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Services.Arm;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests.SiteExtensions
{
    [KuduXunitTestClass]
    public class SiteExtensionApiFacts
    {
        private static readonly Dictionary<string, string> _galleryInstalledExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"sitereplicator", "Site"},
            {"websitelogs", "Log Browser"},
            {"phpmyadmin", "php"},
            {"AzureImageOptimizer", "Image Optimizer"},
            {"AzureMinifier", "Minifier"},
        };

        [Theory]
        [InlineData(null, "sitereplicator", "site replicator")]    // default site extension endpoint (v2)
        [InlineData("https://api.nuget.org/v3/index.json", "filecounter", "file counter")]    // v3 endpoint
        public async Task SiteExtensionV2AndV3FeedTests(string feedEndpoint, string testPackageId, string searchString)
        {
            TestTracer.Trace("Testing against feed: '{0}'", feedEndpoint);

            const string appName = "SiteExtensionV2AndV3FeedTests";
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                // list package
                TestTracer.Trace("Search extensions by id: '{0}'", testPackageId);
                HttpResponseMessage responseMessage = await manager.GetRemoteExtensions(filter: searchString, feedUrl: feedEndpoint);
                IEnumerable<SiteExtensionInfo> results = await responseMessage.Content.ReadAsAsync<IEnumerable<SiteExtensionInfo>>();
                Assert.True(results.Count() > 0, string.Format("GetRemoteExtensions for '{0}' package result should > 0", testPackageId));

                // get package
                TestTracer.Trace("Get an extension by id: '{0}'", testPackageId);
                SiteExtensionInfo result = await (await manager.GetRemoteExtension(testPackageId, feedUrl: feedEndpoint)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(testPackageId, result.Id);

                string testPackageVersion = result.Version;

                TestTracer.Trace("Get an extension by id: '{0}' and version: '{1}'", testPackageId, testPackageVersion);
                result = await (await manager.GetRemoteExtension(testPackageId, version: testPackageVersion, feedUrl: feedEndpoint)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(testPackageId, result.Id);
                Assert.Equal(testPackageVersion, result.Version);

                // install
                TestTracer.Trace("Install an extension by id: '{0}' and version: '{1}'", testPackageId, testPackageVersion);
                responseMessage = await manager.InstallExtension(testPackageId, version: testPackageVersion, feedUrl: feedEndpoint);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(testPackageId, result.Id);
                Assert.Equal(testPackageVersion, result.Version);
                Assert.False(responseMessage.Headers.Contains(Constants.SiteOperationHeaderKey)); // only ARM request will have SiteOperation header

                // uninstall
                TestTracer.Trace("Uninstall an extension by id: '{0}'", testPackageId);
                bool deleteResult = await (await manager.UninstallExtension(testPackageId)).Content.ReadAsAsync<bool>();
                Assert.True(deleteResult);
            });
        }

        [Fact]
        public async Task SiteExtensionBasicTests()
        {
            const string appName = "SiteExtensionBasicTests";
            const string installationArgument = "arg0";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                // list
                List<SiteExtensionInfo> results = await (await manager.GetRemoteExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.True(results.Any(), "GetRemoteExtensions expects results > 0");

                // pick site extension
                var expectedId = _galleryInstalledExtensions.Keys.ToArray()[new Random().Next(_galleryInstalledExtensions.Count)];
                var expectedInstallationArgs = installationArgument;
                var expected = results.Find(ext => string.Equals(ext.Id, expectedId, StringComparison.OrdinalIgnoreCase));
                Assert.True(expected != null, string.Format(CultureInfo.InvariantCulture, "Should able to find {0} from search result", expectedId));
                TestTracer.Trace("Testing Against Site Extension '{0}' - '{1}'", expectedId, expected.Version);

                // get
                TestTracer.Trace("Perform GetRemoteExtension with id '{0}' only", expectedId);
                HttpResponseMessage responseMessage = await manager.GetRemoteExtension(expectedId);
                SiteExtensionInfo result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // clear local extensions
                TestTracer.Trace("Clear all installed extensions.");
                results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                HttpResponseMessage deleteResponseMessage = null;
                foreach (var ext in results)
                {
                    deleteResponseMessage = await manager.UninstallExtension(ext.Id);
                    Assert.True(await deleteResponseMessage.Content.ReadAsAsync<bool>(), "Delete must return true");
                    Assert.True(deleteResponseMessage.Headers.Contains(Constants.RequestIdHeader));
                }

                // install/update
                TestTracer.Trace("Perform InstallExtension with id '{0}' and installationArgs '{1}'", expectedId, installationArgument);
                responseMessage = await manager.InstallExtension(expected.Id, installationArgs:installationArgument);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);
                Assert.Equal(expectedInstallationArgs, result.InstallationArgs);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // list
                TestTracer.Trace("Perform GetLocalExtensions with no parameter");
                results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.True(results.Any(), "GetLocalExtensions expects results > 0");

                // get
                TestTracer.Trace("Perform GetLocalExtension with id '{0}' only.", expectedId);
                responseMessage = await manager.GetLocalExtension(expected.Id);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expectedInstallationArgs, result.InstallationArgs);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // delete
                TestTracer.Trace("Perform UninstallExtension with id '{0}' only.", expectedId);
                responseMessage = await manager.UninstallExtension(expected.Id);
                bool deleteResult = await responseMessage.Content.ReadAsAsync<bool>();
                Assert.True(deleteResult, "Delete must return true");
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // list installed
                TestTracer.Trace("Verify only '{0}' is installed", expectedId);
                results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.False(results.Exists(ext => ext.Id == expected.Id), "After deletion extension " + expected.Id + " should not exist.");

                // install from non-default endpoint
                responseMessage = await manager.InstallExtension("filecounter", version: "1.0.19", feedUrl: "https://www.nuget.org/api/v2/", installationArgs:installationArgument);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal("filecounter", result.Id);
                Assert.Equal("1.0.19", result.Version);
                Assert.Equal("https://www.nuget.org/api/v2/", result.FeedUrl);
                Assert.Equal(expectedInstallationArgs, result.InstallationArgs);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // update site extension installed from non-default endpoint with no installation arguments
                responseMessage = await manager.InstallExtension("filecounter");
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal("filecounter", result.Id);
                Assert.Equal("https://www.nuget.org/api/v2/", result.FeedUrl);
                Assert.True(string.IsNullOrWhiteSpace(result.InstallationArgs));
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // update site extension installed using installation arguments
                responseMessage = await manager.InstallExtension("filecounter", installationArgs:installationArgument);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal("filecounter", result.Id);
                Assert.Equal("https://www.nuget.org/api/v2/", result.FeedUrl);
                Assert.Equal(expectedInstallationArgs, result.InstallationArgs);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
            });
        }

        [Fact]
        public async Task SiteExtensionShouldDeployWebJobs()
        {
            const string appName = "SiteExtensionShouldDeployWebJobs";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                TestTracer.Trace("Install site extension with jobs");
                await manager.InstallExtension("filecounterwithwebjobs", null, "https://www.myget.org/F/amitaptest/");

                TestTracer.Trace("Verify jobs were deployed");
                await OperationManager.AttemptAsync(async () =>
                {
                    var continuousJobs = (await appManager.JobsManager.ListContinuousJobsAsync()).ToArray();
                    Assert.Equal(1, continuousJobs.Length);
                    Assert.Equal("filecounterwithwebjobs(cjoba)", continuousJobs[0].Name);
                    TestTracer.Trace("Job status - {0}", continuousJobs[0].Status);
                    Assert.Equal("PendingRestart", continuousJobs[0].Status);
                }, 100, 500);

                TriggeredJob[] triggeredJobs;
                await OperationManager.AttemptAsync(async () =>
                {
                    triggeredJobs = (await appManager.JobsManager.ListTriggeredJobsAsync()).ToArray();
                    Assert.Equal(2, triggeredJobs.Length);
                    Assert.Equal("filecounterwithwebjobs(tjoba)", triggeredJobs[0].Name);
                    Assert.Equal("filecounterwithwebjobs(tjobb)", triggeredJobs[1].Name);
                }, 100, 500);

                TestTracer.Trace("Uninstall site extension with jobs");
                await manager.UninstallExtension("filecounterwithwebjobs");

                TestTracer.Trace("Verify jobs removed");
                await OperationManager.AttemptAsync(async () =>
                {
                    var continuousJobs2 = (await appManager.JobsManager.ListContinuousJobsAsync()).ToArray();
                    Assert.Equal(0, continuousJobs2.Length);

                    triggeredJobs = (await appManager.JobsManager.ListTriggeredJobsAsync()).ToArray();
                    Assert.Equal(0, triggeredJobs.Length);
                }, 100, 500);
            });
        }

        [Theory]
        [InlineData(null, "sitereplicator")]    // default site extension endpoint (v2)
        [InlineData("https://api.nuget.org/v3/index.json", "filecounter")]    // v3 endpoint
        public async Task SiteExtensionInstallUninstallAsyncTest(string feedEndpoint, string testPackageId)
        {
            TestTracer.Trace("Testing against feed: '{0}'", feedEndpoint);

            const string appName = "SiteExtensionInstallUninstallAsyncTest";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                // Get the latest package, make sure that call will return right away when we try to update
                TestTracer.Trace("Get latest package '{0}' from '{1}'", testPackageId, feedEndpoint);
                SiteExtensionInfo latestPackage = await (await manager.GetRemoteExtension(testPackageId, feedUrl: feedEndpoint)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(testPackageId, latestPackage.Id);

                // install from non-default endpoint
                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                TestTracer.Trace("Install package '{0}'-'{1}' fresh from '{2}' async", testPackageId, latestPackage.Version, feedEndpoint);
                HttpResponseMessage responseMessage = await manager.InstallExtension(id: testPackageId, version: latestPackage.Version, feedUrl: feedEndpoint);
                ArmEntry<SiteExtensionInfo> armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(string.Empty, armResult.Location);   // test "x-ms-geo-location" header is empty, same value should be assign to "Location"
                Assert.Equal(HttpStatusCode.Created, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("Poll for status. Expecting 200 response eventually with site operation header.");
                responseMessage = await PollAndVerifyAfterArmInstallation(manager, testPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                // after successfully installed, should return SiteOperationHeader to notify GEO to restart website
                Assert.True(responseMessage.Headers.Contains(Constants.SiteOperationHeaderKey));
                Assert.Equal(feedEndpoint, armResult.Properties.FeedUrl);
                Assert.Equal(latestPackage.Version, armResult.Properties.Version);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("Get '{0}' again. Expecting 200 response without site operation header", testPackageId);
                responseMessage = await manager.GetLocalExtension(testPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                // get again shouldn`t see SiteOperationHeader anymore
                Assert.False(responseMessage.Headers.Contains(Constants.SiteOperationHeaderKey));
                Assert.Equal(feedEndpoint, armResult.Properties.FeedUrl);
                Assert.Equal(latestPackage.Version, armResult.Properties.Version);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("Try to update package '{0}' without given a feed", testPackageId);
                // Not passing feed endpoint will default to feed endpoint from installed package
                // Update should return right away, expecting code to look up from feed that store in local package
                // since we had installed the latest package, there is nothing to update.
                // We shouldn`t see any site operation header value
                // And there is no polling, since it finished within 15 seconds
                responseMessage = await manager.InstallExtension(testPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(string.Empty, armResult.Location);   // test "x-ms-geo-location" header is empty, same value should be assign to "Location"
                Assert.Equal(latestPackage.Id, armResult.Properties.Id);
                Assert.Equal(feedEndpoint, armResult.Properties.FeedUrl);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                // no installation operation happened, shouldn`t see SiteOperationHeader return
                Assert.False(responseMessage.Headers.Contains(Constants.SiteOperationHeaderKey));

                TestTracer.Trace("Uninstall '{0}' async", testPackageId);
                responseMessage = await manager.UninstallExtension(testPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Null(armResult);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("Get '{0}' again. Expecting 404.", testPackageId);
                var ex = await KuduAssert.ThrowsUnwrappedAsync<HttpUnsuccessfulRequestException>(async () =>
                {
                    await manager.GetLocalExtension(testPackageId);
                });
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
            });
        }

        [Fact]
        public async Task SiteExtensionGetArmTest()
        {
            const string appName = "SiteExtensionGetAsyncTest";
            const string searchString = "file counter";
            const string externalPackageId = "filecounter";
            const string externalFeed = "https://api.nuget.org/v3/index.json";
            const string installationArgument = "arg0";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                TestTracer.Trace("GetRemoteExtensions with Arm header, expecting site extension info will be wrap inside Arm envelop");
                HttpResponseMessage responseMessage = await manager.GetRemoteExtensions(searchString, true, externalFeed);
                ArmListEntry<SiteExtensionInfo> armResultList = await responseMessage.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResultList);
                Assert.NotEmpty(armResultList.Value);
                Assert.NotNull(armResultList.Value.Where(item => string.Equals(externalPackageId, item.Properties.Id, StringComparison.OrdinalIgnoreCase)));
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("GetRemoteExtension with Arm header, expecting site extension info will be wrap inside Arm envelop");
                responseMessage = await manager.GetRemoteExtension(externalPackageId, feedUrl: externalFeed);
                ArmEntry<SiteExtensionInfo> armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResult);
                Assert.Equal(externalPackageId, armResult.Properties.Id);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, false);
                responseMessage = await manager.InstallExtension(externalPackageId, feedUrl: externalFeed, installationArgs: installationArgument);
                SiteExtensionInfo syncResult = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(externalPackageId, syncResult.Id);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                TestTracer.Trace("GetLocalExtensions (no filter) with Arm header, expecting site extension info will be wrap inside Arm envelop");
                responseMessage = await manager.GetLocalExtensions();
                armResultList = await responseMessage.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResultList);
                Assert.NotEmpty(armResultList.Value);
                Assert.NotNull(armResultList.Value.Where(item => string.Equals(externalPackageId, item.Properties.Id, StringComparison.OrdinalIgnoreCase)));
                Assert.Equal(Constants.SiteExtensionProvisioningStateSucceeded, armResultList.Value.First<ArmEntry<SiteExtensionInfo>>().Properties.ProvisioningState);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                foreach (var item in armResultList.Value)
                {
                    Assert.Equal(Constants.SiteExtensionProvisioningStateSucceeded, item.Properties.ProvisioningState);
                    Assert.Equal(installationArgument, item.Properties.InstallationArgs);
                }

                TestTracer.Trace("GetLocalExtensions (with filter) with Arm header, expecting site extension info will be wrap inside Arm envelop");
                responseMessage = await manager.GetLocalExtensions(externalPackageId);
                armResultList = await responseMessage.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResultList);
                Assert.NotEmpty(armResultList.Value);
                Assert.NotNull(armResultList.Value.Where(item => string.Equals(externalPackageId, item.Properties.Id, StringComparison.OrdinalIgnoreCase)));
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                foreach (var item in armResultList.Value)
                {
                    Assert.Equal(Constants.SiteExtensionProvisioningStateSucceeded, item.Properties.ProvisioningState);
                    Assert.Equal(installationArgument, item.Properties.InstallationArgs);
                }
            });
        }

        [Theory]
        [InlineData(null, "sitereplicator", "filecounter", "filecountermvc")]    // default site extension endpoint (v2)
        // [InlineData("https://api.nuget.org/v3/index.json", "bootstrap", "knockoutjs", "angularjs")]    // v3 endpoint
        public async Task SiteExtensionParallelInstallationTest(string feedEndpoint, string testPackageId1, string testPackageId2, string testPackageId3)
        {
            const string appName = "SiteExtensionParallelInstallationTest";
            string[] packageIds = { testPackageId1, testPackageId2, testPackageId3 };
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>();
                List<Task<HttpResponseMessage>> pollingTasks = new List<Task<HttpResponseMessage>>();

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                TestTracer.Trace("Start parallel install multiple extensions ...");
                foreach (var packageId in packageIds)
                {
                    tasks.Add(Task.Run<HttpResponseMessage>(async () =>
                    {
                        TestTracer.Trace("Install package '{0}' fresh from '{1}' async", packageId, feedEndpoint);
                        HttpResponseMessage responseMessage = await manager.InstallExtension(id: packageId, feedUrl: feedEndpoint);
                        ArmEntry<SiteExtensionInfo> armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                        Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                        Assert.Equal(HttpStatusCode.Created, responseMessage.StatusCode);
                        return responseMessage;
                    }));
                }

                await Task.WhenAll(tasks);

                foreach (var packageId in packageIds)
                {
                    pollingTasks.Add(Task.Run<HttpResponseMessage>(async () =>
                    {
                        TestTracer.Trace("Poll '{0}' for status for '{1}'. Expecting 200 response eventually with site operation header.", feedEndpoint, packageId);
                        HttpResponseMessage responseMessage = await PollAndVerifyAfterArmInstallation(manager, packageId);
                        ArmEntry<SiteExtensionInfo> armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                        Assert.Equal(feedEndpoint, armResult.Properties.FeedUrl);
                        Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                        Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                        return responseMessage;
                    }));
                }

                await Task.WhenAll(pollingTasks);

                TestTracer.Trace("Installation are done, expecting only one site operation header return (trigger site restart)");
                // should only trigger restart once
                IEnumerable<Task<HttpResponseMessage>> responses = pollingTasks.Where((task) =>
                {
                    return task.Result.Headers.Contains(Constants.SiteOperationHeaderKey);
                });

                Assert.Equal(1, responses.Count());
            });
        }

        [Fact]
        public async Task SiteExtensionShouldNotSeeButAbleToInstallUnlistedPackage()
        {
            const string appName = "SiteExtensionShouldNotSeeUnlistPackage";
            const string externalPackageId = "SimpleSite";
            const string unlistedVersion = "3.0.0";
            const string latestListedVersion = "2.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                HttpResponseMessage response = await manager.GetRemoteExtension(externalPackageId, feedUrl: externalFeed);
                SiteExtensionInfo info = await response.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.NotEqual(unlistedVersion, info.Version);

                response = await manager.GetRemoteExtension(externalPackageId, version: unlistedVersion, feedUrl: externalFeed);
                info = await response.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(unlistedVersion, info.Version);

                response = await manager.GetRemoteExtensions(externalPackageId, allowPrereleaseVersions: true, feedUrl: externalFeed);
                List<SiteExtensionInfo> infos = await response.Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.NotEmpty(infos);
                foreach (var item in infos)
                {
                    Assert.NotEqual(unlistedVersion, item.Version);
                }

                response = await manager.InstallExtension(externalPackageId, feedUrl: externalFeed);
                info = await response.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(externalPackageId, info.Id);
                Assert.Equal(latestListedVersion, info.Version);
                Assert.Equal(externalFeed, info.FeedUrl);

                TestTracer.Trace("Should able to installed unlisted package if specify version");
                response = await manager.InstallExtension(externalPackageId, version: unlistedVersion, feedUrl: externalFeed);
                info = await response.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(externalPackageId, info.Id);
                Assert.Equal(unlistedVersion, info.Version);
                Assert.Equal(externalFeed, info.FeedUrl);

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, isArmRequest: true);
                response = await manager.GetRemoteExtension(externalPackageId, feedUrl: externalFeed);
                ArmEntry<SiteExtensionInfo> armInfo = await response.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.NotEqual(unlistedVersion, armInfo.Properties.Version);

                response = await manager.GetRemoteExtensions(externalPackageId, allowPrereleaseVersions: true, feedUrl: externalFeed);
                ArmListEntry<SiteExtensionInfo> armInfos = await response.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                Assert.NotEmpty(armInfos.Value);
                foreach (var item in armInfos.Value)
                {
                    Assert.NotEqual(unlistedVersion, item.Properties.Version);
                }

                response = await manager.InstallExtension(externalPackageId, feedUrl: externalFeed);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                await PollAndVerifyAfterArmInstallation(manager, externalPackageId);
                response = await manager.GetLocalExtension(externalPackageId);
                armInfo = await response.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(externalPackageId, armInfo.Properties.Id);
                Assert.Equal(latestListedVersion, armInfo.Properties.Version);
                Assert.Equal(externalFeed, armInfo.Properties.FeedUrl);

                response = await manager.InstallExtension(externalPackageId, version: unlistedVersion, feedUrl: externalFeed);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                await PollAndVerifyAfterArmInstallation(manager, externalPackageId);
                response = await manager.GetLocalExtension(externalPackageId);
                armInfo = await response.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(externalPackageId, armInfo.Properties.Id);
                Assert.Equal(unlistedVersion, armInfo.Properties.Version);
                Assert.Equal(externalFeed, armInfo.Properties.FeedUrl);
            });
        }

        [Fact]
        public async Task SiteExtensionNormalizedVersionTests()
        {
            const string appName = "SiteExtensionNormalizedVersionTests";
            const string externalPackageId = "SimpleSite";
            const string firstInstallVersion = "1.0";
            const string firstInstallNormalizedVersion = "1.0.0";
            const string latestListedNormalizedVersion = "2.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                var response = await manager.InstallExtension(externalPackageId, version: firstInstallVersion, feedUrl: externalFeed);
                var info = await response.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(externalPackageId, info.Id);
                Assert.NotEqual(firstInstallVersion, info.Version); // return value is normalized
                Assert.Equal(firstInstallNormalizedVersion, info.Version);

                // Update to latest version
                response = await manager.InstallExtension(externalPackageId, feedUrl: externalFeed);
                info = await response.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(latestListedNormalizedVersion, info.Version);
            });
        }

        [Fact]
        public async Task SiteExtensionInstallPackageToWebRootTests()
        {
            const string appName = "SiteExtensionInstallPackageToWebRootTests";
            const string externalPackageId = "SimpleSvc";
            const string externalPackageVersion = "1.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            // site extension 'webrootxdttest' search for xdt files under site extension 'webrootxdttest' folder, and print out xdt content onto page
            const string externalPackageWithXdtId = "webrootxdttest";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                // install/update
                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}'", externalPackageId, externalPackageVersion, externalFeed);
                HttpResponseMessage responseMessage = await manager.InstallExtension(externalPackageId, externalPackageVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);
                SiteExtensionInfo result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(externalPackageId, result.Id);
                Assert.Equal(externalPackageVersion, result.Version);
                Assert.Equal(externalFeed, result.FeedUrl);

                TestTracer.Trace("GET request to verify package content has been copied to wwwroot");
                HttpClient client = new HttpClient();
                responseMessage = await client.GetAsync(appManager.SiteUrl);
                string responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.NotNull(responseContent);
                Assert.True(responseContent.Contains(@"<h3>Site for testing</h3>"));

                TestTracer.Trace("GetLocalExtension should return WebRoot type SiteExtensionInfo");
                responseMessage = await manager.GetLocalExtension(externalPackageId);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(SiteExtensionInfo.SiteExtensionType.WebRoot, result.Type);

                responseMessage = await manager.GetLocalExtensions(externalPackageId);
                var results = await responseMessage.Content.ReadAsAsync<List<SiteExtensionInfo>>();
                foreach (var item in results)
                {
                    if (string.Equals(externalPackageId, item.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(SiteExtensionInfo.SiteExtensionType.WebRoot, item.Type);
                    }
                }

                // delete
                TestTracer.Trace("Perform UninstallExtension with id '{0}' only.", externalPackageId);
                responseMessage = await manager.UninstallExtension(externalPackageId);
                bool deleteResult = await responseMessage.Content.ReadAsAsync<bool>();
                Assert.True(deleteResult, "Delete must return true");

                TestTracer.Trace("GET request to verify package content has been removed wwwroot");
                responseMessage = await client.GetAsync(appManager.SiteUrl);
                Assert.Equal(HttpStatusCode.Forbidden, responseMessage.StatusCode);

                // install package that with xdt file
                TestTracer.Trace("Perform InstallExtension with id '{0}' from '{1}'", externalPackageWithXdtId, externalFeed);
                responseMessage = await manager.InstallExtension(externalPackageWithXdtId, feedUrl: externalFeed, type: SiteExtensionInfo.SiteExtensionType.WebRoot);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(externalPackageWithXdtId, result.Id);
                Assert.Equal(externalFeed, result.FeedUrl);

                TestTracer.Trace("GET request to verify package content has been copied to wwwroot");
                responseMessage = await client.GetAsync(appManager.SiteUrl);
                responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.NotNull(responseContent);
                Assert.True(responseContent.Contains(@"1 files"));
                Assert.True(responseContent.Contains(@"site\path\shall\not\be\found")); // xdt content
            });
        }

        [Fact]
        public async Task SiteExtensionInstallPackageToWebRootAsyncTests()
        {
            const string appName = "SiteExtensionInstallPackageToWebRootAsyncTests";
            const string externalPackageId = "SimpleSvc";
            const string externalPackageVersion = "1.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            // site extension 'webrootxdttest' search for xdt files under site extension 'webrootxdttest' folder, and print out xdt content onto page
            const string externalPackageWithXdtId = "webrootxdttest";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                // install/update
                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}'", externalPackageId, externalPackageVersion, externalFeed);
                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                HttpResponseMessage responseMessage = await manager.InstallExtension(externalPackageId, externalPackageVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);
                ArmEntry<SiteExtensionInfo> armResult = null;
                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    TestTracer.Trace("Installation done within 15 seconds, no polling needed.");
                    armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                }
                else
                {
                    Assert.Equal(HttpStatusCode.Created, responseMessage.StatusCode);
                    responseMessage = await PollAndVerifyAfterArmInstallation(manager, externalPackageId);
                    armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                }

                // shouldn`t see restart header since package doesn`t come with XDT
                Assert.False(responseMessage.Headers.Contains(Constants.SiteOperationHeaderKey), "Must not contain restart header");
                Assert.Equal(externalFeed, armResult.Properties.FeedUrl);
                Assert.Equal(externalPackageVersion, armResult.Properties.Version);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace("GET request to verify package content has been copied to wwwroot");
                HttpClient client = new HttpClient();
                responseMessage = await client.GetAsync(appManager.SiteUrl);
                string responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.NotNull(responseContent);
                Assert.True(responseContent.Contains(@"<h3>Site for testing</h3>"));

                TestTracer.Trace("GetLocalExtension should return WebRoot type SiteExtensionInfo");
                responseMessage = await manager.GetLocalExtension(externalPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(SiteExtensionInfo.SiteExtensionType.WebRoot, armResult.Properties.Type);

                responseMessage = await manager.GetLocalExtensions(externalPackageId);
                var results = await responseMessage.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                foreach (var item in results.Value)
                {
                    if (string.Equals(externalPackageId, item.Properties.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(SiteExtensionInfo.SiteExtensionType.WebRoot, item.Properties.Type);
                    }
                }

                // delete
                TestTracer.Trace("Perform UninstallExtension with id '{0}' only.", externalPackageId);
                responseMessage = await manager.UninstallExtension(externalPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Null(armResult);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace("GET request to verify package content has been removed wwwroot");
                responseMessage = await client.GetAsync(appManager.SiteUrl);
                Assert.Equal(HttpStatusCode.Forbidden, responseMessage.StatusCode);

                // install package that with xdt file
                TestTracer.Trace("Perform InstallExtension with id '{0}' from '{1}'", externalPackageWithXdtId, externalFeed);
                responseMessage = await manager.InstallExtension(externalPackageWithXdtId, feedUrl: externalFeed, type: SiteExtensionInfo.SiteExtensionType.WebRoot);
                Assert.Equal(HttpStatusCode.Created, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                // package come with XDT, which would require site restart
                TestTracer.Trace("Poll for status. Expecting 200 response eventually with site operation header.");
                responseMessage = await PollAndVerifyAfterArmInstallation(manager, externalPackageWithXdtId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                // after successfully installed, should return SiteOperationHeader to notify GEO to restart website
                Assert.True(responseMessage.Headers.Contains(Constants.SiteOperationHeaderKey));
                Assert.Equal(externalFeed, armResult.Properties.FeedUrl);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace("GET request to verify package content has been copied to wwwroot");
                responseMessage = await client.GetAsync(appManager.SiteUrl);
                responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.NotNull(responseContent);
                Assert.True(responseContent.Contains(@"1 files"));
                Assert.True(responseContent.Contains(@"site\path\shall\not\be\found")); // xdt content
            });
        }

        [Fact]
        public async Task SiteExtensionNugetOrgTest()
        {
            const string appName = "SiteExtensionNugetOrgTest";
            const string nonSiteExtensionPackage = "NewRelic.Azure.WebSites";
            const string siteExtensionPackage = "filecounter";  // known-good entry on www.nuget.org
            const string feedEndpoint = "https://www.nuget.org/api/v2";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await CleanSiteExtensions(manager);

                // Ensure we don't find non-AzureSiteExtension package on www.nuget.org
                TestTracer.Trace("Search extensions by id: '{0}'", nonSiteExtensionPackage);
                HttpResponseMessage responseMessage = await manager.GetRemoteExtensions(filter: nonSiteExtensionPackage, feedUrl: feedEndpoint);
                IEnumerable<SiteExtensionInfo> results = await responseMessage.Content.ReadAsAsync<IEnumerable<SiteExtensionInfo>>();
                Assert.True(results.Count() == 0, string.Format("We shouldn't find package '{0}' on www.nuget.org", nonSiteExtensionPackage));

                // Ensure we *do* find a known-AzureSiteExtension package on www.nuget.org
                TestTracer.Trace("Search extensions by id: '{0}'", siteExtensionPackage);
                responseMessage = await manager.GetRemoteExtensions(filter: siteExtensionPackage, feedUrl: feedEndpoint);
                results = await responseMessage.Content.ReadAsAsync<IEnumerable<SiteExtensionInfo>>();
                Assert.True(results.Count() >= 0, string.Format("We should find package '{0}' on www.nuget.org", siteExtensionPackage));
            });
        }

        private async Task<HttpResponseMessage> PollAndVerifyAfterArmInstallation(RemoteSiteExtensionManager manager, string packageId)
        {
            TestTracer.Trace("Polling for status for '{0}'", packageId);
            DateTime start = DateTime.UtcNow;
            HttpResponseMessage responseMessage = null;

            UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
            do
            {
                responseMessage = await manager.GetLocalExtension(packageId);

                if (HttpStatusCode.Created == responseMessage.StatusCode)
                {
                    TestTracer.Trace("Action is on-going, wait for 3 seconds for next poll. Package: '{0}'", packageId);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
                else if (HttpStatusCode.OK == responseMessage.StatusCode)
                {
                    TestTracer.Trace("Action is done successfully for package '{0}'.", packageId);
                    break;
                }

                Assert.True(
                    HttpStatusCode.Created == responseMessage.StatusCode
                    || HttpStatusCode.OK == responseMessage.StatusCode, string.Format(CultureInfo.InvariantCulture, "Action failed. Package: '{0}'", packageId));

            } while ((DateTime.UtcNow - start).TotalSeconds < 120);

            TestTracer.Trace("Polled for '{0}' seconds. Response status is '{1}'. Package: '{2}'", (DateTime.UtcNow - start).TotalSeconds, responseMessage.StatusCode, packageId);
            Assert.True(HttpStatusCode.OK == responseMessage.StatusCode, string.Format(CultureInfo.InvariantCulture, "Action failed. Package: {0}", packageId));
            return responseMessage;
        }

        private static void UpdateHeaderIfGoingToBeArmRequest(HttpClient client, bool isArmRequest)
        {
            var containsArmHeader = client.DefaultRequestHeaders.Contains(ArmUtils.GeoLocationHeaderKey);

            if (isArmRequest && !containsArmHeader)
            {
                client.DefaultRequestHeaders.Add(ArmUtils.GeoLocationHeaderKey, string.Empty);
            }
            else if (!isArmRequest && containsArmHeader)
            {
                client.DefaultRequestHeaders.Remove(ArmUtils.GeoLocationHeaderKey);
            }
        }

        public static async Task CleanSiteExtensions(RemoteSiteExtensionManager manager)
        {
            try
            {
                TestTracer.Trace("Clean site extensions");
                List<SiteExtensionInfo> results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                List<Task> tasks = new List<Task>();
                foreach (var ext in results)
                {
                    tasks.Add(manager.UninstallExtension(ext.Id));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                TestTracer.Trace("Failed to perform cleanup, might cause testcase failure. {0}", ex.ToString());
            }
        }
    }
}