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
                VfsControllerBaseTest suite = new VfsControllerBaseTest(appManager.LiveScmVfsManager, testConflictingUpdates: true);

                // Act + Assert
                suite.RunIntegrationTest();
            });
        }
    }
}
