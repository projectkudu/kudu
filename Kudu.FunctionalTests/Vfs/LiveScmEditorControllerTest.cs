using System.Threading.Tasks;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
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
