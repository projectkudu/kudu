using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client.Editor;
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
                var suite = new VfsControllerSuite(appManager.VfsManager, testConflictingUpdates: false);

                // Act + Assert
                await suite.RunIntegrationTest();
            });
        }


        private sealed class VfsControllerSuite : VfsControllerBaseTest
        {
            public VfsControllerSuite(RemoteVfsManager client, bool testConflictingUpdates, RemoteVfsManager deploymentClient = null)
                : base(client, testConflictingUpdates, deploymentClient)
            {
            }

            public async override Task RunIntegrationTest()
            {
                await base.RunIntegrationTest();

                // Check that we can create a directory
                string putDirectoryAddressWithTerminatingSlash = BaseAddress + '/' + Path.GetRandomFileName() + '/';
                TestTracer.Trace("=== Check that we can create a directory");
                var response = await HttpPutAsync(putDirectoryAddressWithTerminatingSlash, CreateUploadContent(new byte[0]));
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }
        }
    }
}
