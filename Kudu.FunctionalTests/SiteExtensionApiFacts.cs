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

                // get
                var expected = results.First();
                var result = await manager.GetRemoteExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);

                // install/update
                result = await manager.InstallExtension(expected);
                Assert.Equal(expected.Id, result.Id);

                // list
                results = await manager.GetLocalExtensions();
                Assert.True(results.Count() > 0, "GetLocalExtensions expects results > 0");

                // get
                result = await manager.GetLocalExtension(expected.Id);
                Assert.Equal(expected.Id, result.Id);

                // delete
                var deleted = await manager.UninstallExtension(expected.Id);
                Assert.True(deleted, "Delete must return true");
            });
        }
    }
}