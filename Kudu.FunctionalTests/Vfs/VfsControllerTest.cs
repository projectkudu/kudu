using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Editor;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
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

        [Fact]
        public async Task VfsSpecialFoldersTest()
        {
            // Arrange
            string appName = "VfsSpecialFolders";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Act + Assert
                Assert.True(appManager.VfsManager.Exists("/vfs/SystemDrive/"), "'/vfs/SystemDrive/' must exists");

                Assert.True(appManager.VfsManager.Exists("/vfs/SystemDrive/windows/"), "'/vfs/SystemDrive/windows/' must exists");

                Assert.False(appManager.VfsManager.Exists("/vfs/SystemDrive/NotFound/"), "'/vfs/SystemDrive/NotFound/' must not exists");

                try
                {
                    string value = await appManager.SettingsManager.GetValue("WEBSITE_NODE_DEFAULT_VERSION");
                    if (!String.IsNullOrEmpty(value))
                    {
                        Assert.True(appManager.VfsManager.Exists("/vfs/LocalSiteRoot/Temp/"), "'/vfs/LocalSiteRoot/Temp/' must exists");
                    }
                }
                catch (HttpUnsuccessfulRequestException ex)
                {
                    if (ex.ResponseMessage.StatusCode != HttpStatusCode.NotFound)
                    {
                        throw;
                    }
                }

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
