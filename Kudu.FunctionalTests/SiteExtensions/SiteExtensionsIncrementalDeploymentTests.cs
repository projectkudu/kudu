using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.SiteExtensions;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests.SiteExtensions
{
    [KuduXunitTestClass]
    public class SiteExtensionsIncrementalDeploymentTests
    {
        [Fact]
        public async Task CanInstallAndUpdate()
        {
            const string appName = "IncrementalDeploymentTests";
            const string packageId = "IncrementalDeployment";
            const string packageOldVersion = "1.0.0";
            const string packageLatestVersion = "2.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                var manager = appManager.SiteExtensionManager;
                await SiteExtensionApiFacts.CleanSiteExtensions(manager);

                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}'", packageId, packageOldVersion, externalFeed);
                HttpResponseMessage responseMessage = await manager.InstallExtension(packageId, packageOldVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                HttpClient client = new HttpClient();
                TestTracer.Trace("Package is installed to wwwroot, perform get to verify content.");
                responseMessage = await client.GetAsync(appManager.SiteUrl + "default.aspx");
                string responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.True(responseContent.Contains("<h1>World's most amazing file counter</h1>"),
                    string.Format(CultureInfo.InvariantCulture, "Not contain expected content. Actual: {0}", responseContent));

                responseMessage = await client.GetAsync(appManager.SiteUrl + "delete.txt");
                responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.Equal("delete", responseContent);

                responseMessage = await client.GetAsync(appManager.SiteUrl + "new.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);

                responseMessage = await manager.InstallExtension(packageId, packageLatestVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                responseMessage = await client.GetAsync(appManager.SiteUrl + "default.aspx");
                responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.True(responseContent.Contains("<h1>World's most amazing file counter v2!</h1>"),
                    string.Format(CultureInfo.InvariantCulture, "Not contain expected content. Actual: {0}", responseContent));

                responseMessage = await client.GetAsync(appManager.SiteUrl + "new.txt");
                responseContent = await responseMessage.Content.ReadAsStringAsync();
                Assert.Equal("new", responseContent);

                responseMessage = await client.GetAsync(appManager.SiteUrl + "delete.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);
            });
        }
    }
}
