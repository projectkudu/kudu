using System;
using System.Linq;
using System.Threading.Tasks;
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
            string appName = "SiteExtensionBasicTests";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;

                // list
                var results = await manager.GetRemoteExtensions();
                Assert.True(results.Count() > 0, "GetRemoteExtensions expects results > 0");

                results = await manager.GetRemoteExtensions(filter: "dummy");
                Assert.True(results.Count() > 0, "GetRemoteExtensions expects results > 0");

                // list
                results = await manager.GetLocalExtensions(update_info: false);
                Assert.True(results.Count() > 0, "GetExtensions expects results > 0");

                // get
                var expected = results.First();
                var result = await manager.GetRemoteExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);

                // get
                result = await manager.GetLocalExtension(expected.Id, update_info: false);
                Assert.Equal(expected.Id, result.Id);

                // install/update
                result = await manager.InstallExtension(expected);
                Assert.Equal(expected.Id, result.Id);

                // delete
                var deleted = await manager.UninstallExtension(expected.Id);
                Assert.True(deleted, "Delete must return true");
            });
        }
    }
}