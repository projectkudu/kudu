using System.Threading.Tasks;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class VfsControllerTest
    {
        [Fact]
        public async Task VfsIntegrationTest()
        {
            // Arrange
            string appName = "VfsIntegrationTest";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                VfsControllerBaseTest suite = new VfsControllerBaseTest(appManager.VfsManager, testConflictingUpdates: false);

                // Act + Assert
                await suite.RunIntegrationTest();
            });
        }
    }
}
