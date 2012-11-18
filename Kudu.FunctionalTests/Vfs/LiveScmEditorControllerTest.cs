using System;
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
            string appName = KuduUtils.GetRandomWebsiteName("VfsScmIntegrationTest");

            ApplicationManager.Run(appName, appManager =>
            {
                string vfsController = String.Format("{0}scmvfs", appManager.ServiceUrl);
                VfsControllerBaseTest suite = new VfsControllerBaseTest(vfsController, testConflictingUpdates: true);

                // Act + Assert
                suite.RunIntegrationTest();
            });
        }
    }
}
