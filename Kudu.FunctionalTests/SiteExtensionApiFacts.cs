using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
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
        [InlineData("https://api.nuget.org/v3/index.json")]
        [InlineData("http://www.nuget.org/api/v2/")]
        public async Task SiteExtensionV2AndV3FeedTests(string feedEndpoint)
        {
            TestTracer.Trace("Testing against feed: '{0}'", feedEndpoint);

            const string appName = "SiteExtensionV2AndV3FeedTests";
            const string testPackageId = "bootstrap";
            const string testPackageVersion = "3.0.0";
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // list package
                TestTracer.Trace("Search extensions by id: '{0}'", testPackageId);
                IEnumerable<SiteExtensionInfo> results = await manager.GetRemoteExtensions(
                    filter: testPackageId,
                    feedUrl: feedEndpoint);
                Assert.True(results.Count() > 0, string.Format("GetRemoteExtensions for '{0}' package result should > 0", testPackageId));

                // get package
                TestTracer.Trace("Get an extension by id: '{0}'", testPackageId);
                SiteExtensionInfo result = await manager.GetRemoteExtension(testPackageId, feedUrl: feedEndpoint);
                Assert.Equal(testPackageId, result.Id);

                TestTracer.Trace("Get an extension by id: '{0}' and version: '{1}'", testPackageId, testPackageVersion);
                result = await manager.GetRemoteExtension(testPackageId, version: testPackageVersion, feedUrl: feedEndpoint);
                Assert.Equal(testPackageId, result.Id);
                Assert.Equal(testPackageVersion, result.Version);

                // install
                TestTracer.Trace("Install an extension by id: '{0}' and version: '{1}'", testPackageId, testPackageVersion);
                HttpResponseResult<SiteExtensionInfo> richResult = await manager.InstallExtension<SiteExtensionInfo>(testPackageId, version: testPackageVersion, feedUrl: feedEndpoint);
                Assert.Equal(testPackageId, richResult.Body.Id);
                Assert.Equal(testPackageVersion, richResult.Body.Version);
                Assert.False(richResult.Headers.ContainsKey(Constants.SiteOperationHeaderKey)); // only ARM request will have SiteOperation header

                // uninstall
                TestTracer.Trace("Uninstall an extension by id: '{0}'", testPackageId);
                Assert.True(await manager.UninstallExtension(testPackageId));
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
                List<SiteExtensionInfo> results = (await manager.GetRemoteExtensions()).ToList();
                Assert.True(results.Any(), "GetRemoteExtensions expects results > 0");

                // pick site extension
                var expectedId = _galleryInstalledExtensions.Keys.ToArray()[new Random().Next(_galleryInstalledExtensions.Count)];
                var expected = results.Find(ext => String.Equals(ext.Id, expectedId, StringComparison.OrdinalIgnoreCase));
                TestTracer.Trace("Testing Against Site Extension {0}", expectedId);

                // get
                var result = await manager.GetRemoteExtension(expectedId);
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);

                // clear local extensions
                results = (await manager.GetLocalExtensions()).ToList();
                foreach (var ext in results)
                {
                    Assert.True(await manager.UninstallExtension(ext.Id), "Delete must return true");
                }

                // install/update
                HttpResponseResult<SiteExtensionInfo> richResult = await manager.InstallExtension<SiteExtensionInfo>(expected.Id);
                Assert.Equal(expected.Id, richResult.Body.Id);
                Assert.Equal(expected.Version, richResult.Body.Version);

                // list
                results = (await manager.GetLocalExtensions()).ToList();
                Assert.True(results.Any(), "GetLocalExtensions expects results > 0");

                // get
                result = await manager.GetLocalExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);

                // delete
                var deleted = await manager.UninstallExtension(expected.Id);
                Assert.True(deleted, "Delete must return true");

                // list installed
                results = (await manager.GetLocalExtensions()).ToList();
                Assert.False(results.Exists(ext => ext.Id == expected.Id), "After deletion extension " + expected.Id + " should not exist.");

                // install from non-default endpoint
                richResult = await manager.InstallExtension<SiteExtensionInfo>("bootstrap", version: "3.0.0", feedUrl: "http://www.nuget.org/api/v2/");
                Assert.Equal("bootstrap", richResult.Body.Id);
                Assert.Equal("3.0.0", richResult.Body.Version);
                Assert.Equal("http://www.nuget.org/api/v2/", richResult.Body.FeedUrl);

                // update site extension installed from non-default endpoint
                richResult = await manager.InstallExtension<SiteExtensionInfo>("bootstrap");
                Assert.Equal("bootstrap", richResult.Body.Id);
                Assert.Equal("http://www.nuget.org/api/v2/", richResult.Body.FeedUrl);
            });
        }

        [Theory]
        [InlineData("https://api.nuget.org/v3/index.json")]
        [InlineData("http://www.nuget.org/api/v2/")]
        public async Task SiteExtensionInstallUpdateIdempotentTest(string feedEndpoint)
        {
            TestTracer.Trace("Testing against feed: '{0}'", feedEndpoint);

            const string appName = "SiteExtensionInstallIdempotentTest";
            const string testPackageId = "bootstrap";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // Get the latest package, make sure that call will return right away when we try to update
                TestTracer.Trace("Get latest package '{0}' from '{1}'", testPackageId, feedEndpoint);
                SiteExtensionInfo latestPackage = await manager.GetRemoteExtension(testPackageId, feedUrl: feedEndpoint);

                TestTracer.Trace("Uninstall package '{0}'", latestPackage.Id);
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
                TestTracer.Trace("Install package '{0}'-'{1}' fresh from '{2}'.", latestPackage.Id, latestPackage.Version, feedEndpoint);
                HttpResponseResult<ArmEntry<SiteExtensionInfo>> richResult = await manager.InstallExtension<ArmEntry<SiteExtensionInfo>>(id: testPackageId, version: latestPackage.Version, feedUrl: feedEndpoint);
                Assert.Equal(latestPackage.Id, richResult.Body.Properties.Id);
                Assert.Equal(latestPackage.Version, richResult.Body.Properties.Version);
                Assert.Equal(feedEndpoint, richResult.Body.Properties.FeedUrl);
                Assert.True(richResult.Headers[Constants.SiteOperationHeaderKey].Contains(Constants.SiteOperationRestart));

                TestTracer.Trace("Try to update package '{0}' without given a feed.", testPackageId);
                // Not passing feed endpoint will default to feed endpoint from installed package
                // Update should return right away, expecting code to look up from feed that store in local package
                // since we had installed the latest package, there is nothing to update.
                // We shouldn`t see any site operation header value
                richResult = await manager.InstallExtension<ArmEntry<SiteExtensionInfo>>(testPackageId);
                Assert.Equal(latestPackage.Id, richResult.Body.Properties.Id);
                Assert.Equal(feedEndpoint, richResult.Body.Properties.FeedUrl);
                Assert.False(richResult.Headers.ContainsKey(Constants.SiteOperationHeaderKey));
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
                List<SiteExtensionInfo> results = (await manager.GetRemoteExtensions()).ToList();
                Assert.True(results.Any(), "GetRemoteExtensions expects results > 0");

                // pick site extension
                var expectedId = _preInstalledExtensions.Keys.ToArray()[new Random().Next(_preInstalledExtensions.Count)];
                var expected = results.Find(ext => String.Equals(ext.Id, expectedId, StringComparison.OrdinalIgnoreCase));
                TestTracer.Trace("Testing Against Site Extension {0}", expectedId);

                // get
                var result = await manager.GetRemoteExtension(expectedId);
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);

                // clear local extensions
                results = (await manager.GetLocalExtensions()).ToList();
                foreach (var ext in results)
                {
                    Assert.True(await manager.UninstallExtension(ext.Id), "Delete must return true");
                }

                // install/update
                HttpResponseResult<SiteExtensionInfo> richResult = await manager.InstallExtension<SiteExtensionInfo>(expected.Id);
                Assert.Equal(expected.Id, richResult.Body.Id);
                Assert.Equal(expected.Version, richResult.Body.Version);

                // list
                results = (await manager.GetLocalExtensions()).ToList();
                Assert.True(results.Any(), "GetLocalExtensions expects results > 0");

                // get
                result = await manager.GetLocalExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);

                // delete
                var deleted = await manager.UninstallExtension(expected.Id);
                Assert.True(deleted, "Delete must return true");

                // list installed
                results = (await manager.GetLocalExtensions()).ToList();
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
                var results = (await manager.GetLocalExtensions()).ToList();
                foreach (var ext in results)
                {
                    await manager.UninstallExtension(ext.Id);
                }

                TestTracer.Trace("Install site extension with jobs");
                await manager.InstallExtension<SiteExtensionInfo>("filecounterwithwebjobs", null, "https://www.myget.org/F/amitaptest/");

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
    }
}