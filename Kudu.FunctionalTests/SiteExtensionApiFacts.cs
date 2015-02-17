using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.SiteExtensions;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Services.Arm;
using Kudu.TestHarness;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    [TestHarnessClassCommand]
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

        private static readonly Dictionary<string, string> _preInstalledExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Monaco", "Visual Studio Online"},
            {"Kudu", "Kudu"},
            {"DaaS", "Site Diagnostics"}
        };

        [Theory]
        [InlineData(null, "sitereplicator", "1.1.2")]    // default site extension endpoint
        [InlineData("https://www.nuget.org/api/v2/", "bootstrap", "3.0.0")]    // external v2 endpoint
        [InlineData("https://api.nuget.org/v3/index.json", "bootstrap", "3.0.0")]    // external v3 endpoint
        public async Task SiteExtensionV2AndV3FeedTests(string feedEndpoint, string testPackageId, string testPackageVersion)
        {
            TestTracer.Trace("Testing against feed: '{0}'", feedEndpoint);

            const string appName = "SiteExtensionV2AndV3FeedTests";
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // list package
                TestTracer.Trace("Search extensions by id: '{0}'", testPackageId);
                HttpResponseMessage responseMessage = await manager.GetRemoteExtensions(filter: testPackageId, feedUrl: feedEndpoint);
                IEnumerable<SiteExtensionInfo> results = await responseMessage.Content.ReadAsAsync<IEnumerable<SiteExtensionInfo>>();
                Assert.True(results.Count() > 0, string.Format("GetRemoteExtensions for '{0}' package result should > 0", testPackageId));

                // get package
                TestTracer.Trace("Get an extension by id: '{0}'", testPackageId);
                SiteExtensionInfo result = await (await manager.GetRemoteExtension(testPackageId, feedUrl: feedEndpoint)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(testPackageId, result.Id);

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

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // list
                List<SiteExtensionInfo> results = await (await manager.GetRemoteExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.True(results.Any(), "GetRemoteExtensions expects results > 0");

                // pick site extension
                var expectedId = _galleryInstalledExtensions.Keys.ToArray()[new Random().Next(_galleryInstalledExtensions.Count)];
                var expected = results.Find(ext => String.Equals(ext.Id, expectedId, StringComparison.OrdinalIgnoreCase));
                TestTracer.Trace("Testing Against Site Extension '{0}' - '{1}'", expectedId, expected.Version);

                // get
                TestTracer.Trace("Perform GetRemoteExtension with id '{0}' only", expectedId);
                HttpResponseMessage responseMessage = await manager.GetRemoteExtension(expectedId);
                SiteExtensionInfo result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);
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
                TestTracer.Trace("Perform InstallExtension with id '{0}' only", expectedId);
                responseMessage = await manager.InstallExtension(expected.Id);
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);
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
                responseMessage = await manager.InstallExtension("bootstrap", version: "3.0.0", feedUrl: "https://www.nuget.org/api/v2/");
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal("bootstrap", result.Id);
                Assert.Equal("3.0.0", result.Version);
                Assert.Equal("https://www.nuget.org/api/v2/", result.FeedUrl);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                // update site extension installed from non-default endpoint
                responseMessage = await manager.InstallExtension("bootstrap");
                result = await responseMessage.Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal("bootstrap", result.Id);
                Assert.Equal("https://www.nuget.org/api/v2/", result.FeedUrl);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
            });
        }

        [Fact]
        public async Task SiteExtensionPreInstalledTests()
        {
            const string appName = "SiteExtensionBasicTests";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // list
                List<SiteExtensionInfo> results = await (await manager.GetRemoteExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.True(results.Any(), "GetRemoteExtensions expects results > 0");

                // pick site extension
                var expectedId = _preInstalledExtensions.Keys.ToArray()[new Random().Next(_preInstalledExtensions.Count)];
                var expected = results.Find(ext => String.Equals(ext.Id, expectedId, StringComparison.OrdinalIgnoreCase));
                TestTracer.Trace("Testing Against Site Extension {0}", expectedId);

                // get
                SiteExtensionInfo result = await (await manager.GetRemoteExtension(expectedId)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);

                // clear local extensions
                results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                bool deleteResult = false;
                foreach (var ext in results)
                {
                    deleteResult = await (await manager.UninstallExtension(ext.Id)).Content.ReadAsAsync<bool>();
                    Assert.True(deleteResult, "Delete must return true");
                }

                // install/update
                result = await (await manager.InstallExtension(expected.Id)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);

                // list
                results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.True(results.Any(), "GetLocalExtensions expects results > 0");

                // get
                result = await (await manager.GetLocalExtension(expected.Id)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(expected.Id, result.Id);

                // delete
                deleteResult = await (await manager.UninstallExtension(expected.Id)).Content.ReadAsAsync<bool>();
                Assert.True(deleteResult, "Delete must return true");

                // list installed
                results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                Assert.False(results.Exists(ext => ext.Id == expected.Id), "After deletion extension " + expected.Id + " should not exist.");
            });
        }

        [Fact]
        public async Task SiteExtensionShouldDeployWebJobs()
        {
            const string appName = "SiteExtensionShouldDeployWebJobs";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                TestTracer.Trace("Clean site extensions");
                List<SiteExtensionInfo> results = await (await manager.GetLocalExtensions()).Content.ReadAsAsync<List<SiteExtensionInfo>>();
                foreach (var ext in results)
                {
                    await manager.UninstallExtension(ext.Id);
                }

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

                var triggeredJobs = (await appManager.JobsManager.ListTriggeredJobsAsync()).ToArray();
                Assert.Equal(2, triggeredJobs.Length);
                Assert.Equal("filecounterwithwebjobs(tjoba)", triggeredJobs[0].Name);
                Assert.Equal("filecounterwithwebjobs(tjobb)", triggeredJobs[1].Name);

                TestTracer.Trace("Uninstall site extension with jobs");
                await manager.UninstallExtension("filecounterwithwebjobs");

                TestTracer.Trace("Verify jobs removed");
                var continuousJobs2 = (await appManager.JobsManager.ListContinuousJobsAsync()).ToArray();
                Assert.Equal(0, continuousJobs2.Length);

                triggeredJobs = (await appManager.JobsManager.ListTriggeredJobsAsync()).ToArray();
                Assert.Equal(0, triggeredJobs.Length);
            });
        }

        [Theory]
        [InlineData(null, "sitereplicator")]    // default site extension endpoint
        [InlineData("https://www.nuget.org/api/v2/", "bootstrap")]    // external v2 endpoint
        [InlineData("https://api.nuget.org/v3/index.json", "bootstrap")]    // external v3 endpoint
        public async Task SiteExtensionInstallUninstallAsyncTest(string feedEndpoint, string testPackageId)
        {
            TestTracer.Trace("Testing against feed: '{0}'", feedEndpoint);

            const string appName = "SiteExtensionInstallUninstallAsyncTest";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // Get the latest package, make sure that call will return right away when we try to update
                TestTracer.Trace("Get latest package '{0}' from '{1}'", testPackageId, feedEndpoint);
                SiteExtensionInfo latestPackage = await (await manager.GetRemoteExtension(testPackageId, feedUrl: feedEndpoint)).Content.ReadAsAsync<SiteExtensionInfo>();
                Assert.Equal(testPackageId, latestPackage.Id);

                TestTracer.Trace("Uninstall package '{0}'", testPackageId);
                try
                {
                    // uninstall package if it is there
                    await manager.UninstallExtension(testPackageId);
                }
                catch
                {
                    // ignore exception
                }

                // install from non-default endpoint
                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                TestTracer.Trace("Install package '{0}'-'{1}' fresh from '{2}' async", testPackageId, latestPackage.Version, feedEndpoint);
                HttpResponseMessage responseMessage = await manager.InstallExtension(id: testPackageId, version: latestPackage.Version, feedUrl: feedEndpoint);
                ArmEntry<SiteExtensionInfo> armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(string.Empty, armResult.Location);   // test "x-ms-geo-location" header is empty, same value should be assign to "Location"
                Assert.Equal(HttpStatusCode.Created, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                //TestTracer.Trace("Poll for status. Expecting 200 response eventually with site operation header.");
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
                responseMessage = await manager.InstallExtension(testPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(string.Empty, armResult.Location);   // test "x-ms-geo-location" header is empty, same value should be assign to "Location"
                Assert.Equal(HttpStatusCode.Created, responseMessage.StatusCode);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("Poll for status. Expecting 200 without site operation header, since no actual installation happened");
                responseMessage = await PollAndVerifyAfterArmInstallation(manager, testPackageId);
                armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
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
            const string externalPackageId = "bootstrap";
            const string externalFeed = "https://api.nuget.org/v3/index.json";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
                TestTracer.Trace("GetRemoteExtensions with Arm header, expecting site extension info will be wrap inside Arm envelop");
                HttpResponseMessage responseMessage = await manager.GetRemoteExtensions(externalPackageId, true, externalFeed);
                ArmListEntry<SiteExtensionInfo> armResultList = await responseMessage.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResultList);
                Assert.NotEmpty(armResultList.Value);
                Assert.Equal(externalPackageId, armResultList.Value.First<ArmEntry<SiteExtensionInfo>>().Properties.Id);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("GetRemoteExtension with Arm header, expecting site extension info will be wrap inside Arm envelop");
                responseMessage = await manager.GetRemoteExtension(externalPackageId, feedUrl: externalFeed);
                ArmEntry<SiteExtensionInfo> armResult = await responseMessage.Content.ReadAsAsync<ArmEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResult);
                Assert.Equal(externalPackageId, armResult.Properties.Id);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                UpdateHeaderIfGoingToBeArmRequest(manager.Client, false);
                responseMessage = await manager.InstallExtension(externalPackageId, feedUrl: externalFeed);
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
                Assert.Equal(externalPackageId, armResultList.Value.First<ArmEntry<SiteExtensionInfo>>().Properties.Id);
                Assert.Equal(Constants.SiteExtensionProvisioningStateSucceeded, armResultList.Value.First<ArmEntry<SiteExtensionInfo>>().Properties.ProvisioningState);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));

                TestTracer.Trace("GetLocalExtensions (with filter) with Arm header, expecting site extension info will be wrap inside Arm envelop");
                responseMessage = await manager.GetLocalExtensions(externalPackageId);
                armResultList = await responseMessage.Content.ReadAsAsync<ArmListEntry<SiteExtensionInfo>>();
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.NotNull(armResultList);
                Assert.NotEmpty(armResultList.Value);
                Assert.Equal(externalPackageId, armResultList.Value.First<ArmEntry<SiteExtensionInfo>>().Properties.Id);
                Assert.True(responseMessage.Headers.Contains(Constants.RequestIdHeader));
                foreach (var item in armResultList.Value)
                {
                    Assert.Equal(Constants.SiteExtensionProvisioningStateSucceeded, item.Properties.ProvisioningState);
                }
            });
        }

        private async Task<HttpResponseMessage> PollAndVerifyAfterArmInstallation(RemoteSiteExtensionManager manager, string packageId)
        {
            TestTracer.Trace("Polling for status");
            DateTime start = DateTime.UtcNow;
            HttpResponseMessage responseMessage = null;

            UpdateHeaderIfGoingToBeArmRequest(manager.Client, true);
            do
            {
                responseMessage = await manager.GetLocalExtension(packageId);

                if (HttpStatusCode.Created == responseMessage.StatusCode)
                {
                    TestTracer.Trace("Action is on-going, wait for 3 seconds for next poll.");
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
                else if (HttpStatusCode.OK == responseMessage.StatusCode)
                {
                    TestTracer.Trace("Action is done successfully.");
                    break;
                }

                Assert.True(
                    HttpStatusCode.Created == responseMessage.StatusCode
                    || HttpStatusCode.OK == responseMessage.StatusCode, "Action failed.");

            } while ((DateTime.UtcNow - start).TotalSeconds < 30);

            TestTracer.Trace("Polled for {0} seconds. Response status is {1}", (DateTime.UtcNow - start).TotalSeconds, responseMessage.StatusCode);
            Assert.True(HttpStatusCode.OK == responseMessage.StatusCode, "Action failed.");
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
    }
}