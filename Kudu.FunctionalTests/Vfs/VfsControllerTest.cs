using System;
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
            string appName = KuduUtils.GetRandomWebsiteName("VfsIntegrationTest");

            ApplicationManager.Run(appName, appManager =>
            {
                string vfsController = String.Format("{0}vfs", appManager.ServiceUrl);
                VfsControllerBaseTest suite = new VfsControllerBaseTest(vfsController, testConflictingUpdates: false);

                // Act + Assert
                suite.RunIntegrationTest();
            });
        }
    }
}
