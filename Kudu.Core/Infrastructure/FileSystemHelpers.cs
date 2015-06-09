using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Kudu.Core.Infrastructure
{
    public static class FileSystemHelpers
    {
        public static IFileSystem Instance { get; set; }

        static FileSystemHelpers()
        {
            Instance = new FileSystem();
        }

        public static Stream CreateFile(string path)
        {
            return Instance.File.Create(path);
        }

        public static void CreateDirectory(string path)
        {
            Instance.Directory.CreateDirectory(path);
        }

        public static string EnsureDirectory(string path)
        {
            if (!DirectoryExists(path))
            {
                CreateDirectory(path);
            }
            return path;
        }

        public static void MoveDirectory(string sourceDirName, string destDirName)
        {
            // Instance.Directory.Move will result in access denied sometime. Do it ourself!

            EnsureDirectory(destDirName);

            string[] files = Instance.Directory.GetFiles(sourceDirName);
            string[] dirs = Instance.Directory.GetDirectories(sourceDirName);

            foreach (var filePath in files)
            {
                var fi = new FileInfo(filePath);
                MoveFile(filePath, Path.Combine(destDirName, fi.Name));
            }

            foreach (var dirPath in dirs)
            {
                var di = new DirectoryInfo(dirPath);
                MoveDirectory(dirPath, Path.Combine(destDirName, di.Name));
                Instance.Directory.Delete(dirPath, false);
            }

            Instance.Directory.Delete(sourceDirName, false);
        }

        public static bool FileExists(string path)
        {
            return Instance.File.Exists(path);
        }

        public static bool DirectoryExists(string path)
        {
            return Instance.Directory.Exists(path);
        }

        public static bool IsSubfolder(string parent, string child)
        {
            // normalize
            string parentPath = Path.GetFullPath(parent).TrimEnd('\\') + '\\';
            string childPath = Path.GetFullPath(child).TrimEnd('\\') + '\\';
            return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
        }

        public static Stream OpenFile(string path, FileMode mode, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None)
        {
            return Instance.File.Open(path, mode, access, share);
        }

        public static Stream OpenRead(string path)
        {
            return Instance.File.OpenRead(path);
        }

        public static string ReadAllText(string path)
        {
            return Instance.File.ReadAllText(path);
        }

        /// <summary>
        /// Replaces File.ReadAllText,
        /// Will do the same thing only this can work on files that are already open (and share read/write).
        /// </summary>
        public static string ReadAllTextFromFile(string path)
        {
            using (Stream fileStream = OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var streamReader = new StreamReader(fileStream);
                return streamReader.ReadToEnd();
            }
        }

        public static Stream OpenWrite(string path)
        {
            return Instance.File.OpenWrite(path);
        }

        public static void WriteAllText(string path, string contents)
        {
            Instance.File.WriteAllText(path, contents);
        }
        public static DateTime GetLastWriteTimeUtc(string path)
        {
            return Instance.File.GetLastWriteTimeUtc(path);
        }

        public static void WriteAllBytes(string path, byte[] contents)
        {
            Instance.File.WriteAllBytes(path, contents);
        }

        /// <summary>
        /// Replaces File.WriteAllText,
        /// Will do the same thing only this can work on files that are already open (and share read/write).
        /// </summary>
        public static void WriteAllTextToFile(string path, string content)
        {
            using (Stream fileStream = OpenFile(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                var streamWriter = new StreamWriter(fileStream);
                streamWriter.Write(content);
                streamWriter.Flush();
            }
        }

        /// <summary>
        /// Replaces File.AppendAllText,
        /// Will do the same thing only this can work on files that are already open (and share read/write).
        /// </summary>
        public static void AppendAllTextToFile(string path, string content)
        {
            using (Stream fileStream = OpenFile(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                var streamWriter = new StreamWriter(fileStream);
                streamWriter.Write(content);
                streamWriter.Flush();
            }
        }

        public static void CopyFile(string sourceFileName, string destFileName, bool overwrite = true)
        {
            Instance.File.Copy(sourceFileName, destFileName, overwrite);
        }

        public static void MoveFile(string sourceFileName, string destFileName)
        {
            FileSystemHelpers.DeleteFileSafe(destFileName);
            Instance.File.Move(sourceFileName, destFileName);
        }

        // From MSDN: http://msdn.microsoft.com/en-us/library/bb762914.aspx
        public static void CopyDirectoryRecursive(string sourceDirPath, string destinationDirPath, bool overwrite = true)
        {
            // Get the subdirectories for the specified directory.
            var sourceDir = new DirectoryInfo(sourceDirPath);

            if (!sourceDir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirPath);
            }

            // If the destination directory doesn't exist, create it.
            if (!DirectoryExists(destinationDirPath))
            {
                CreateDirectory(destinationDirPath);
            }

            // Get the files in the directory and copy them to the new location.
            foreach (FileSystemInfo sourceFileSystemInfo in sourceDir.EnumerateFileSystemInfos())
            {
                var sourceFile = sourceFileSystemInfo as FileInfo;
                if (sourceFile != null)
                {
                    string destinationFilePath = Path.Combine(destinationDirPath, sourceFile.Name);
                    Instance.File.Copy(sourceFile.FullName, destinationFilePath, overwrite);
                }
                else
                {
                    var sourceSubDir = sourceFileSystemInfo as DirectoryInfo;
                    if (sourceSubDir != null)
                    {
                        // Copy sub-directories and their contents to new location.
                        string destinationSubDirPath = Path.Combine(destinationDirPath, sourceSubDir.Name);
                        CopyDirectoryRecursive(sourceSubDir.FullName, destinationSubDirPath, overwrite);
                    }
                }
            }
        }

        public static void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
        {
            Instance.File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        public static DateTime GetDirectoryLastWriteTimeUtc(string path)
        {
            return Instance.Directory.GetLastWriteTimeUtc(path);
        }

        public static void SetDirectoryLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
        {
            Instance.Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        public static FileInfoBase FileInfoFromFileName(string fileName)
        {
            return Instance.FileInfo.FromFileName(fileName);
        }

        public static DirectoryInfoBase DirectoryInfoFromDirectoryName(string path)
        {
            return Instance.DirectoryInfo.FromDirectoryName(path);
        }

        public static string GetFullPath(string path)
        {
            return Instance.Path.GetFullPath(path);
        }

        public static string[] GetFileSystemEntries(string path)
        {
            return Instance.Directory.GetFileSystemEntries(path);
        }

        public static string[] GetDirectories(string path)
        {
            return Instance.Directory.GetDirectories(path);
        }

        public static string[] GetFiles(string path, string pattern)
        {
            return Instance.Directory.GetFiles(path, pattern);
        }

        public static IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            if (!Directory.Exists(path))
            {
                return Enumerable.Empty<string>();
            }

            // Only lookup of type *.extension or path\file (no *) is supported
            if (lookupList.Any(lookup => lookup.LastIndexOf('*') > 0))
            {
                throw new NotSupportedException("lookup with a '*' that is not the first character is not supported");
            }

            lookupList = lookupList.Select(lookup => lookup.TrimStart('*')).ToArray();

            return Directory.EnumerateFiles(path, "*.*", searchOption)
                            .Where(filePath => lookupList.Any(lookup => filePath.EndsWith(lookup, StringComparison.OrdinalIgnoreCase)));
        }

        public static void DeleteFile(string path)
        {
            Instance.File.Delete(path);
        }

        public static void DeleteFileSafe(string path)
        {
            var info = Instance.FileInfo.FromFileName(path);
            DeleteFileSystemInfo(info, ignoreErrors: true);
        }

        public static void DeleteDirectorySafe(string path, bool ignoreErrors = true)
        {
            DeleteFileSystemInfo(Instance.DirectoryInfo.FromDirectoryName(path), ignoreErrors);
        }

        public static void DeleteDirectoryContentsSafe(string path, bool ignoreErrors = true)
        {
            DeleteDirectoryContentsSafe(Instance.DirectoryInfo.FromDirectoryName(path), ignoreErrors);
        }

        private static void DeleteDirectoryContentsSafe(DirectoryInfoBase directoryInfo, bool ignoreErrors)
        {
            try
            {
                if (directoryInfo.Exists)
                {
                    foreach (var fsi in directoryInfo.GetFileSystemInfos())
                    {
                        DeleteFileSystemInfo(fsi, ignoreErrors);
                    }
                }
            }
            catch
            {
                if (!ignoreErrors) throw;
            }
        }

        private static void DeleteFileSystemInfo(FileSystemInfoBase fileSystemInfo, bool ignoreErrors)
        {
            if (!fileSystemInfo.Exists)
            {
                return;
            }

            try
            {
                fileSystemInfo.Attributes = FileAttributes.Normal;
            }
            catch
            {
                if (!ignoreErrors) throw;
            }

            var directoryInfo = fileSystemInfo as DirectoryInfoBase;

            if (directoryInfo != null)
            {
                DeleteDirectoryContentsSafe(directoryInfo, ignoreErrors);
            }

            DoSafeAction(fileSystemInfo.Delete, ignoreErrors);
        }

        private static void DoSafeAction(Action action, bool ignoreErrors)
        {
            try
            {
                OperationManager.Attempt(action);
            }
            catch
            {
                if (!ignoreErrors) throw;
            }
        }
    }
}
