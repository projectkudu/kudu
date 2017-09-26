using System.IO;
using System.IO.Compression;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class PushDeploymentTests
    {
        [Fact]
        public void TestPushDeployment()
        {
            ApplicationManager.Run("TestPushDeployment", appManager =>
            {
                string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDirectory);
                string tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string foundContent;
                try
                {
                    var testFileName = "TestFile." + System.Guid.NewGuid().ToString("N") + ".txt";
                    var testFileLocalPath = Path.Combine(tempDirectory, testFileName);
                    var testFileContent = "Hello World with a guid\n" + System.Guid.NewGuid().ToString("N");

                    TestTracer.Trace("Push-deploying zip with file {0}.", testFileName);
                    File.WriteAllText(testFileLocalPath, testFileContent);
                    ZipFile.CreateFromDirectory(tempDirectory, tempZipPath);
                    appManager.PushDeploymentManager.PushDeployFromFile(tempZipPath, doAsync: true);

                    TestTracer.Trace("Verifying second file {0} is in uploaded.", testFileName);
                    foundContent = appManager.VfsWebRootManager.ReadAllText(testFileName);
                    Assert.Equal(testFileContent, foundContent);

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
