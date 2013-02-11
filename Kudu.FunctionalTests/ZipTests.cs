using System.IO;
using Ionic.Zip;
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
                string tempZipPath = Path.GetTempFileName();

                try
                {
                    // Create file on server using vfs
                    appManager.VfsWebRootManager.WriteAllText("foo.txt", "Hello zip");

                    // Make sure it's part of the zip we get
                    using (var zipFile = ZipFile.Read(appManager.ZipManager.GetZipStream("site")))
                    {
                        zipFile.ExtractAll(tempDirectory);

                        string settings = File.ReadAllText(Path.Combine(tempDirectory, "wwwroot", "foo.txt"));
                        Assert.Contains("Hello zip", settings);
                    }

                    string barFile = Path.Combine(tempDirectory, "wwwroot", "bar.txt");
                    File.WriteAllText(barFile, "Kudu zip");

                    using (var zipFile2 = new ZipFile())
                    {
                        zipFile2.AddDirectory(tempDirectory);
                        zipFile2.Save(tempZipPath);
                    }

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

