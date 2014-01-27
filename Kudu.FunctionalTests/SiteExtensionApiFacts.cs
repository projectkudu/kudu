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
                var results = await manager.GetExtensions();
                Assert.True(results.Count() > 0, "GetExtensions expects results > 0");

                // get
                var expected = results.First();
                var result = await manager.GetExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);

                // install
                result = await manager.InstallExtension(expected);
                Assert.Equal(expected.Id, result.Id);

                // update
                result = await manager.UpdateExtension(expected);
                Assert.Equal(expected.Id, result.Id);

                // update
                var deleted = await manager.UninstallExtension(expected.Id);
                Assert.True(deleted, "Delete must return true");
            });
        }
    }
}