using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public static class ZipArchiveExtensions
    {
        public static void AddDirectory(this ZipArchive zipArchive, string directoryPath, ITracer tracer, string directoryNameInArchive = "")
        {
            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(directoryPath));
            zipArchive.AddDirectory(directoryInfo, tracer, directoryNameInArchive);
        }

        public static void AddDirectory(this ZipArchive zipArchive, DirectoryInfoBase directory, ITracer tracer, string directoryNameInArchive)
        {
            bool any = false;
            foreach (var info in directory.GetFileSystemInfos())
            {
                any = true;
                var subDirectoryInfo = info as DirectoryInfoBase;
                if (subDirectoryInfo != null)
                {
                    string childName = ForwardSlashCombine(directoryNameInArchive, subDirectoryInfo.Name);
                    zipArchive.AddDirectory(subDirectoryInfo, tracer, childName);
                }
                else
                {
                    zipArchive.AddFile((FileInfoBase)info, tracer, directoryNameInArchive);
                }
            }

            if (!any)
            {
                // If the directory did not have any files or folders, add a entry for it
                zipArchive.CreateEntry(EnsureTrailingSlash(directoryNameInArchive));
            }
        }

        private static string ForwardSlashCombine(string part1, string part2)
        {
            return Path.Combine(part1, part2).Replace('\\', '/');
        }

        public static void AddFile(this ZipArchive zipArchive, string filePath, ITracer tracer, string directoryNameInArchive = "")
        {
            var fileInfo = new FileInfoWrapper(new FileInfo(filePath));
            zipArchive.AddFile(fileInfo, tracer, directoryNameInArchive);
        }

        public static void AddFile(this ZipArchive zipArchive, FileInfoBase file, ITracer tracer, string directoryNameInArchive)
        {
            Stream fileStream = null;
            try
            {
                fileStream = file.OpenRead();
            }
            catch (Exception ex)
            {
                // tolerate if file in use.
                // for simplicity, any exception.
                tracer.TraceError(String.Format("{0}, {1}", file.FullName, ex));
                return;
            }

            try
            {
                string fileName = ForwardSlashCombine(directoryNameInArchive, file.Name);
                ZipArchiveEntry entry = zipArchive.CreateEntry(fileName, CompressionLevel.Fastest);
                entry.LastWriteTime = file.LastWriteTime;

                using (Stream zipStream = entry.Open())
                {
                    fileStream.CopyTo(zipStream);
                }
            }
            finally
            {
                fileStream.Dispose();
            }
        }

        public static void AddFile(this ZipArchive zip, string fileName, string fileContent)
        {
            ZipArchiveEntry entry = zip.CreateEntry(fileName, CompressionLevel.Fastest);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(fileContent);
            }
        }

        public static void Extract(this ZipArchive archive, string directoryName)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string path = Path.Combine(directoryName, entry.FullName);
                if (entry.Length == 0 && (path.EndsWith("/", StringComparison.Ordinal) || path.EndsWith("\\", StringComparison.Ordinal)))
                {
                    // Extract directory
                    FileSystemHelpers.CreateDirectory(path);
                }
                else
                {
                    FileInfoBase fileInfo = FileSystemHelpers.FileInfoFromFileName(path);

                    if (!fileInfo.Directory.Exists)
                    {
                        fileInfo.Directory.Create();
                    }

                    using (Stream zipStream = entry.Open(),
                                  fileStream = fileInfo.Open(FileMode.Create, FileAccess.Write))
                    {
                        zipStream.CopyTo(fileStream);
                    }
                }
            }
        }

        private static string EnsureTrailingSlash(string input)
        {
            return input.EndsWith("/", StringComparison.Ordinal) ? input : input + "/";
        }
    }
}