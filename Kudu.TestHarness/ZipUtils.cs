using System.IO;
using Ionic.Zip;

namespace Kudu.TestHarness
{
    public class ZipUtils
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

        public static void Unzip(Stream zipStream, Stream targetStream)
        {
            using (ZipFile zip = ZipFile.Read(zipStream))
            {
                foreach (ZipEntry file in zip)
                {
                    file.Extract(targetStream);
                }
            }
        }
    }
}
