using System.IO;
using System.Linq;
using Ionic.Zip;

namespace Kudu.FunctionalTests.Infrastructure
{
    internal class Utils
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

        public static void DeleteDirectory(string targetDir)
        {
            string[] files = Directory.GetFiles(targetDir);
            string[] dirs = Directory.GetDirectories(targetDir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(targetDir, false);
        }

        public static bool DirectoriesEqual(string path1, string path2)
        {
            string[] files1 = Directory.GetFiles(path1);
            string[] files2 = Directory.GetFiles(path2);
            if (files1.Length != files2.Length)
            {
                return false;
            }

            foreach (string file in files1)
            {
                var token = file.LastIndexOf(@"\");
                var fileName = file.Substring(token + 1);
                if (!Directory.EnumerateFiles(path2, fileName).Any())
                {
                    return false;
                }
            }

            string path;
            foreach (string dir in Directory.EnumerateDirectories(path1))
            {
                path = Path.Combine(path2, dir);
                if (!Directory.Exists(path))
                {
                    return false;
                }

                if (!DirectoriesEqual(Path.Combine(path1, dir), path))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
