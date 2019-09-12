using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests.SiteExtensions
{
    [KuduXunitTestClass]
    public class SiteExtensionsIncrementalDeploymentTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CanInstallAndUpdate(bool useSiteExtensionV1)
        {
            const string appName = "IncrementalDeploymentTests";
            const string packageId = "IncrementalDeployment";
            const string packageOldVersion = "1.0.0";
            const string packageLatestVersion = "2.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (useSiteExtensionV1)
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.UseSiteExtensionV1, "1");
                }

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

                TestTracer.Trace("Perform update with id '{0}', version '{1}' to WebRoot", packageId, packageLatestVersion);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CanInstallAndUpdateWithoutType(bool useSiteExtensionV1)
        {
            const string appName = "IncrementalDeploymentTests";
            const string packageId = "IncrementalDeployment";
            const string packageOldVersion = "1.0.0";
            const string packageLatestVersion = "2.0.0";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (useSiteExtensionV1)
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.UseSiteExtensionV1, "1");
                }

                var manager = appManager.SiteExtensionManager;
                await SiteExtensionApiFacts.CleanSiteExtensions(manager);

                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}' to WebRoot", packageId, packageOldVersion, externalFeed);
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

                TestTracer.Trace("Perform update with id '{0}', version '{1}' to WebRoot without specifiy type", packageId, packageLatestVersion);
                responseMessage = await manager.InstallExtension(packageId, packageLatestVersion, externalFeed);
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

        /// <summary>
        /// <para>Test package that are not zip by nuget tool</para>
        /// <para>Differences are, package zip by nuget tool will not include 'folder', others might</para>
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CanInstallAndUpdateWithAbnormalPackage(bool useSiteExtensionV1)
        {
            const string appName = "IncrementalDeploymentWithComplexPackageTests";
            const string packageId = "microsoft.com.azuremediaservicesconnector";
            const string packageOldVersion = "0.1.1";
            const string packageNewerVersion = "0.1.3";
            const string packageLatestVersion = "0.1.4";
            const string externalFeed = "https://www.myget.org/F/simplesvc/";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (useSiteExtensionV1)
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.UseSiteExtensionV1, "1");
                }

                var manager = appManager.SiteExtensionManager;
                await SiteExtensionApiFacts.CleanSiteExtensions(manager);

                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}'", packageId, packageOldVersion, externalFeed);
                HttpResponseMessage responseMessage = await manager.InstallExtension(packageId, packageOldVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                HttpClient client = new HttpClient();
                TestTracer.Trace("Package is installed to wwwroot, perform get to verify content.");

                TestTracer.Trace("Should see web.config.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + "web.config.txt");
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace(@"Should see NewFolder/NewFile.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"NewFolder/NewFile.txt");
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}'", packageId, packageNewerVersion, externalFeed);
                responseMessage = await manager.InstallExtension(packageId, packageNewerVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);

                TestTracer.Trace(@"Should NOT see web.config.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + "web.config.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);

                TestTracer.Trace(@"Should NOT see NewFolder/NewFile.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"NewFolder/NewFile.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);

                TestTracer.Trace(@"Should see metadata/UIDefinition.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"metadata/UIDefinition.txt");
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace("Perform InstallExtension with id '{0}', version '{1}' from '{2}'", packageId, packageLatestVersion, externalFeed);
                responseMessage = await manager.InstallExtension(packageId, packageLatestVersion, externalFeed, SiteExtensionInfo.SiteExtensionType.WebRoot);

                TestTracer.Trace(@"Should NOT see web.config.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + "web.config.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);

                TestTracer.Trace(@"Should NOT see NewFolder/NewFile.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"NewFolder/NewFile.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);

                TestTracer.Trace(@"Should NOT see metadata/UIDefinition.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"metadata/UIDefinition.txt");
                Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);

                TestTracer.Trace(@"Should see metadata/NewFile.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"metadata/NewFile.txt");
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                TestTracer.Trace(@"Should see apiapp.txt");
                responseMessage = await client.GetAsync(appManager.SiteUrl + @"apiapp.txt");
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
            });
        }
    }
}
