using System.IO;
using System.IO.Compression;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class ZipTests
    {
        [Fact]
        public void TestZipController()
        {
            ApplicationManager.Run("TestZip", appManager =>
            {
                string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDirectory);
                string tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                try
                {
                    // Create file on server using vfs
                    appManager.VfsWebRootManager.WriteAllText("foo.txt", "Hello zip");

                    // Make sure it's part of the zip we get
                    using (var zipFile = new ZipArchive(appManager.ZipManager.GetZipStream("site")))
                    {
                        zipFile.ExtractToDirectory(tempDirectory);

                        string settings = File.ReadAllText(Path.Combine(tempDirectory, "wwwroot", "foo.txt"));
                        Assert.Contains("Hello zip", settings);
                    }

                    string barFile = Path.Combine(tempDirectory, "wwwroot", "bar.txt");
                    File.WriteAllText(barFile, "Kudu zip");

                    ZipFile.CreateFromDirectory(tempDirectory, tempZipPath);

                    // Upload a zip with an additional file
                    appManager.ZipManager.PutZipFile("site", tempZipPath);

                    // Use vfs to make sure it's there
                    string barContent = appManager.VfsWebRootManager.ReadAllText("bar.txt");
                    Assert.Contains("Kudu zip", barContent);
                }
                finally
                {
                    Directory.Delete(tempDirectory, recursive: true);
                    File.Delete(tempZipPath);
                }
            });
        }
    }
}

