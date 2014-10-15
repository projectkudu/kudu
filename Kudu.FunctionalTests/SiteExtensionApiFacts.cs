using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.SiteExtensions;
using Kudu.TestHarness;
using Xunit;

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
                result = await manager.InstallExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);

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
                result = await manager.InstallExtension("bootstrap", version: "3.0.0", feedUrl: "http://www.nuget.org/api/v2/");
                Assert.Equal("bootstrap", result.Id);
                Assert.Equal("3.0.0", result.Version);
                Assert.Equal("http://www.nuget.org/api/v2/", result.FeedUrl);

                // update site extension installed from non-default endpoint
                result = await manager.InstallExtension("bootstrap");
                Assert.Equal("bootstrap", result.Id);
                Assert.Equal("http://www.nuget.org/api/v2/", result.FeedUrl);
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
                result = await manager.InstallExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Version, result.Version);

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
    }
}