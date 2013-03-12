using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class LiveScmEditorControllerTest
    {
        [Fact]
        public void VfsScmIntegrationTest()
        {
            // Arrange
            string appName = "VfsScmIntegrationTest";

            ApplicationManager.Run(appName, appManager =>
            {
                VfsControllerBaseTest suite = new VfsControllerBaseTest(appManager.LiveScmVfsManager, testConflictingUpdates: true, deploymentClient: appManager.LiveScmVfsManager);

                // Act + Assert
                suite.RunIntegrationTest().Wait();
            });
        }
    }
}
