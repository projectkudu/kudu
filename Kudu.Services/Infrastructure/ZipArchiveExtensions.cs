using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using Kudu.Services.Infrastructure;

namespace Kudu.Services
{
    public static class ZipArchiveExtensions
    {
        public static void AddDirectory(this ZipArchive zipArchive, string directoryPath, string directoryNameInArchive)
        {
            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(directoryPath));
            zipArchive.AddDirectory(directoryInfo, directoryNameInArchive);
        }

        public static void AddDirectory(this ZipArchive zipArchive, DirectoryInfoBase directory, string directoryNameInArchive)
        {
            bool any = false;
            foreach (var info in directory.GetFileSystemInfos())
            {
                any = true;
                var subDirectoryInfo = info as DirectoryInfoBase;
                if (subDirectoryInfo != null)
                {
                    string childName = Path.Combine(directoryNameInArchive, subDirectoryInfo.Name);
                    zipArchive.AddDirectory(subDirectoryInfo, childName);
                }
                else
                {
                    zipArchive.AddFile((FileInfoBase)info, directoryNameInArchive);
                }
            }

            if (!any)
            {
                // If the directory did not have any files, add a entry for it
                zipArchive.CreateEntry(UriHelper.EnsureTrailingSlash(directoryNameInArchive));
            }
        }

        public static void AddFile(this ZipArchive zipArchive, string filePath, string directoryNameInArchive)
        {
            var fileInfo = new FileInfoWrapper(new FileInfo(filePath));
            zipArchive.AddFile(fileInfo, directoryNameInArchive);
        }

        public static void AddFile(this ZipArchive zipArchive, FileInfoBase file, string directoryNameInArchive)
        {
            string fileName = Path.Combine(directoryNameInArchive, file.Name);
            var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Fastest);
            using (Stream zipStream = entry.Open(),
                          fileStream = file.OpenRead())
            {
                fileStream.CopyTo(zipStream);
            }
        }

        public static void Extract(this ZipArchive archive, FileSystem fileSystem, string directoryName)
        {
            foreach (var entry in archive.Entries)
            {
                string path = Path.Combine(directoryName, entry.FullName.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (entry.Length == 0)
                {
                    // Extract directory
                    fileSystem.Directory.CreateDirectory(path);
                }
                else
                {
                    FileInfoBase fileInfo = fileSystem.FileInfo.FromFileName(path);

                    if (!fileInfo.Directory.Exists)
                    {
                        fileInfo.Directory.Create();
                    }

                    using (Stream zipStream = entry.Open(),
                                  fileStream = fileInfo.OpenWrite())
                    {
                        zipStream.CopyTo(fileStream);
                    }
                }
            }
        }
    }
}