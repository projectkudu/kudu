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

                // get
                var targets = new Dictionary<string, string> 
                {
                    {"sitereplicator", "Site"},
                    {"websitelogs", "Log Browser"},
                    {"phpmyadmin", "php"},
                    {"AzureImageOptimizer", "Image Optimizer"},
                    {"AzureMinifier", "Minifier"},
                };
                var expectedId = targets.Keys.ToArray()[new Random().Next(targets.Count)];
                var expected = results.Find(ext => ext.Id == expectedId);
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

                // get
                var targets = new Dictionary<string, string> 
                {
                    {"monaco", "Visual Studio Online"},
                    {"kudu", "Kudu"},
                    {"daas", "Site Diagnostics"}
                };
                var expectedId = targets.Keys.ToArray()[new Random().Next(targets.Count)];
                var expected = results.Find(ext => ext.Id == expectedId);
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