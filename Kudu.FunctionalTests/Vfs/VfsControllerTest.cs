using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class VfsControllerTest
    {
        [Fact]
        public void VfsIntegrationTest()
        {
            // Arrange
            string appName = "VfsIntegrationTest";

            ApplicationManager.Run(appName, appManager =>
            {
                VfsControllerBaseTest suite = new VfsControllerBaseTest(appManager.VfsManager, testConflictingUpdates: false);

                // Act + Assert
                suite.RunIntegrationTest();
            });
        }
    }
}
