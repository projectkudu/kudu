using System.Threading.Tasks;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class LiveScmEditorControllerTest
    {
        [Fact]
        public async Task VfsScmIntegrationTest()
        {
            // Arrange
            string appName = "VfsScmIntegrationTest";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                VfsControllerBaseTest suite = new VfsControllerBaseTest(appManager.LiveScmVfsManager, testConflictingUpdates: true, deploymentClient: appManager.VfsManager);

                // Act + Assert
                await suite.RunIntegrationTest();
            });
        }
    }
}
