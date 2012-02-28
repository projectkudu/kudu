using Ionic.Zip;

namespace Kudu.TestHarness
{
    internal class ZipUtils
    {
        public static void Unzip(string zipPath, string targetDir)
        {
            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry file in zip)
                {
                    file.Extract(targetDir, ExtractExistingFileAction.OverwriteSilently);
                }
            }
        }       
    }
}
