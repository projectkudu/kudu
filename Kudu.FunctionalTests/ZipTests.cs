using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class ZipTests
    {
        [Fact]
        public void TestZipController()
        {
            ApplicationManager.Run("TestZip", appManager =>
            {
                string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDirectory);
                string tempZip1Path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string tempZip2Path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string foundContent;
                try
                {
                    var siteRoot = "site";
                    var wwwRoot = "wwwroot";
                    var testFile1Name = "TestFile1." + System.Guid.NewGuid().ToString("N") + ".txt";
                    var testFile1Content = "Hello World\n" + System.Guid.NewGuid().ToString("N");
                    var testFile1LocalPath = Path.Combine(tempDirectory, wwwRoot, testFile1Name);

                    TestTracer.Trace("Creating first test file {0} is on the server.", testFile1Name);
                    appManager.VfsWebRootManager.WriteAllText(testFile1Name, testFile1Content);

                    TestTracer.Trace("Verifying first file {0} is in downloaded.", testFile1Name);
                    using (var zipFile = new ZipArchive(appManager.ZipManager.GetZipStream(siteRoot)))
                    {
                        zipFile.ExtractToDirectory(tempDirectory);
                        foundContent = File.ReadAllText(testFile1LocalPath);
                        Assert.Equal(testFile1Content, foundContent);
                    }

                    var testFile2Name = "TestFile2." + System.Guid.NewGuid().ToString("N") + ".txt";
                    var testFile2LocalPath = Path.Combine(tempDirectory, wwwRoot, testFile2Name);
                    var testFile2InitialContent = "Hello World with a guid\n" + System.Guid.NewGuid().ToString("N");
                    var testFile2UpdatedContent = "Hello World without a guid";

                    // Make sure our second version of the file is smaller so we can see bugs in file overwrite.
                    Assert.True(testFile2UpdatedContent.Length < testFile2InitialContent.Length);

                    TestTracer.Trace("Uploading second file {0}.", testFile2Name);
                    File.WriteAllText(testFile2LocalPath, testFile2InitialContent);
                    ZipFile.CreateFromDirectory(tempDirectory, tempZip1Path);
                    appManager.ZipManager.PutZipFile(siteRoot, tempZip1Path);

                    TestTracer.Trace("Verifying second file {0} is in uploaded.", testFile2Name);
                    foundContent = appManager.VfsWebRootManager.ReadAllText(testFile2Name);
                    Assert.Equal(testFile2InitialContent, foundContent);

                    TestTracer.Trace("Uploading zip with modified second file and missing first file.", testFile2UpdatedContent);
                    File.Delete(testFile1LocalPath);
                    File.WriteAllText(testFile2LocalPath, testFile2UpdatedContent);
                    ZipFile.CreateFromDirectory(tempDirectory, tempZip2Path);
                    appManager.ZipManager.PutZipFile(siteRoot, tempZip2Path);

                    TestTracer.Trace("Verifying second file is in uploaded and modified correctly.");
                    foundContent = appManager.VfsWebRootManager.ReadAllText(testFile2Name);
                    Assert.Equal(testFile2UpdatedContent, foundContent);

                    // This is expected because our zip controller does not delete files
                    // that are missing from the zip if they are already existing on the server.
                    TestTracer.Trace("Verifying first file still on server.");
                    foundContent = appManager.VfsWebRootManager.ReadAllText(testFile1Name);
                    Assert.Equal(testFile1Content, foundContent);

                }
                finally
                {
                    Directory.Delete(tempDirectory, recursive: true);
                    File.Delete(tempZip1Path);
                    File.Delete(tempZip2Path);
                }
            });
        }

        [Fact]
        public async Task TestZipControllerPackageUri()
        {
            if (!TryGetZipUrl(out Uri packageUri))
            {
                throw new KuduXunitTestSkippedException("No ZipUrl is defined");
            }

            await ApplicationManager.RunAsync("TestZipPackageUri", async appManager =>
            {
                var payload = new
                {
                    packageUri = packageUri.AbsoluteUri
                };

                using (var response = await appManager.ZipManager.Client.PutAsJsonAsync(string.Empty, payload))
                {
                    response.EnsureSuccessStatusCode();
                }

                var result = await appManager.VfsManager.ReadAllTextAsync("test/hello.txt");
                Assert.Equal("hello", result);

                await appManager.VfsManager.DeleteAsync("test/hello.txt");
            });
        }

        [Fact]
        public async Task TestZipControllerARMPackageUri()
        {
            if (!TryGetZipUrl(out Uri packageUri))
            {
                throw new KuduXunitTestSkippedException("No ZipUrl is defined");
            }

            await ApplicationManager.RunAsync("TestZipControllerARMPackageUri", async appManager =>
            {
                var payload = new
                {
                    packageUri = packageUri.AbsoluteUri,
                    path = "data/temp"
                };

                var content = new StringContent(JsonConvert.SerializeObject(new { properties = payload }), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Put, string.Empty) { Content = content };
                request.Headers.Add("x-ms-geo-location", "eastus");

                using (var response = await appManager.ZipManager.Client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                }

                var result = appManager.VfsManager.ReadAllText("data/temp/test/hello.txt");
                Assert.Equal("hello", result);

                await appManager.VfsManager.DeleteAsync("data/temp/test/hello.txt");
            });
        }

        private bool TryGetZipUrl(out Uri packageUri)
        {
            var assembly = typeof(VfsControllerBaseTest).Assembly;
            string prefix = typeof(VfsControllerBaseTest).Namespace;
            using (var reader = new StreamReader(assembly.GetManifestResourceStream(prefix + ".Vfs.ZipUrl.txt")))
            {
                return Uri.TryCreate(reader.ReadToEnd(), UriKind.Absolute, out packageUri);
            }
        }
    }
}

