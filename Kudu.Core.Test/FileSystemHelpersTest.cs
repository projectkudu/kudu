using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class FileSystemHelpersTest
    {
        [Fact]
        public void EnsureDirectoryCreatesDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(false);

            string path = FileSystemHelpers.EnsureDirectory(fileSystem.Object, "foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Once());
        }

        [Fact]
        public void EnsureDirectoryDoesNotCreateDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(true);

            string path = FileSystemHelpers.EnsureDirectory(fileSystem.Object, "foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Never());
        }

        [Fact]
        public void SmartCopyCopiesFilesFromSourceToDestination()
        {
            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\test\", filePaths: new[] { @"a.txt", "b.txt", ".bar" });
            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\foo\", exists: false);
            FileSystemHelpers.SmartCopy(@"a:\test\",
                                        @"b:\foo\",
                                        null,
                                        sourceDirectory.Directory,
                                        destinationDirectory.Directory,
                                        path => GetDirectory(path, exists: false).Directory,
                                        skipOldFiles: false);

            destinationDirectory.VerifyCreated();
            sourceDirectory.VerifyCopied("a.txt", @"b:\foo\a.txt");
            sourceDirectory.VerifyCopied("b.txt", @"b:\foo\b.txt");
            sourceDirectory.VerifyNotCopied(".bar", @"b:\foo\.bar");
        }

        [Fact]
        public void SmartCopyDeletesFilesThatDontExistInSourceIfNoPrevious()
        {
            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\test\", filePaths: new[] { @"a.txt", "b.txt" });
            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\foo\", filePaths: new[] { "c.txt" });
            FileSystemHelpers.SmartCopy(@"a:\test\",
                                        @"b:\foo\",
                                         null,
                                         sourceDirectory.Directory,
                                         destinationDirectory.Directory,
                                         path => GetDirectory(path, exists: false).Directory,
                                         skipOldFiles: false);

            sourceDirectory.VerifyCopied("a.txt", @"b:\foo\a.txt");
            sourceDirectory.VerifyCopied("b.txt", @"b:\foo\b.txt");
            destinationDirectory.VerifyDeleted("c.txt");
        }

        [Fact]
        public void SmartCopyOnlyDeletesFilesThatDontExistInSourceIfAlsoInPrevious()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                { "c.txt" }
            };

            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\test\", filePaths: new[] { @"a.txt", "b.txt" });
            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\foo\", filePaths: new[] { "c.txt", "generated.log" });
            FileSystemHelpers.SmartCopy(@"a:\test\",
                                        @"b:\foo\",
                                         paths.Contains,
                                         sourceDirectory.Directory,
                                         destinationDirectory.Directory,
                                         path => GetDirectory(path, exists: false).Directory,
                                         skipOldFiles: false);

            sourceDirectory.VerifyCopied("a.txt", @"b:\foo\a.txt");
            sourceDirectory.VerifyCopied("b.txt", @"b:\foo\b.txt");
            destinationDirectory.VerifyDeleted("c.txt");
            destinationDirectory.VerifyNotDeleted("generated.log");
        }

        [Fact]
        public void SmartCopyOnlyCopiesFileIfNewerAndSkipOldFilesChecked()
        {
            var previousPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                { "a.txt" }
            };

            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\source\", filePaths: new[] { @"a.txt" });
            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\target\", filePaths: new[] { "a.txt" });

            sourceDirectory.Files["a.txt"].Setup(m => m.LastWriteTimeUtc).Returns(new DateTime(2010, 11, 19));
            destinationDirectory.Files["a.txt"].Setup(m => m.LastWriteTimeUtc).Returns(new DateTime(2011, 11, 19));

            FileSystemHelpers.SmartCopy(@"a:\source\",
                                        @"b:\target\",
                                         previousPaths.Contains,
                                         sourceDirectory.Directory,
                                         destinationDirectory.Directory,
                                         path => GetDirectory(path, exists: false).Directory,
                                         skipOldFiles: true);

            sourceDirectory.VerifyNotCopied("a.txt");
        }

        [Fact]
        public void SmartCopyCopiesSubDirectoriesAndFiles()
        {
            DirectoryWrapper sourceSub = GetDirectory(@"a:\source\sub1", filePaths: new[] { "b.js" });
            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\source\",
                                                            filePaths: new[] { @"a.txt" },
                                                            directories: new[] { sourceSub });

            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\target\", exists: false);

            FileSystemHelpers.SmartCopy(@"a:\source\",
                                        @"b:\target\",
                                         null,
                                         sourceDirectory.Directory,
                                         destinationDirectory.Directory,
                                         path =>
                                         {
                                             var newDir = GetDirectory(path, exists: false);
                                             string shortName = GetShortName(destinationDirectory.Directory.FullName, path);
                                             destinationDirectory.Directories[shortName] = newDir;
                                             return newDir.Directory;
                                         },
                                         skipOldFiles: false);

            destinationDirectory.VerifyCreated();
            destinationDirectory.VerifyCreated("sub1");
            sourceDirectory.VerifyCopied("a.txt", @"b:\target\a.txt");
            sourceSub.VerifyCopied("b.js", @"b:\target\sub1\b.js");
        }

        [Fact]
        public void SmartCopyDeletesSubDirectoryIfNoPreviousAndDirectoryDoesnotExistInSource()
        {
            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\source\", filePaths: new[] { "b.txt" });

            DirectoryWrapper destinationSub = GetDirectory(@"b:\target\sub3", filePaths: new[] { "o.js" });
            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\target\", directories: new[] { destinationSub });

            FileSystemHelpers.SmartCopy(@"a:\source\",
                                        @"b:\target\",
                                         null,
                                         sourceDirectory.Directory,
                                         destinationDirectory.Directory,
                                         path => GetDirectory(path).Directory,
                                         skipOldFiles: false);

            destinationSub.VerifyDeleted();
            sourceDirectory.VerifyCopied("b.txt", @"b:\target\b.txt");
        }

        [Fact]
        public void SmartCopyOnlyDeletesSubDirectoryIfExistsInPrevious()
        {
            var previousPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                { @"sub" },
                { @"sub\b.txt" }
            };

            DirectoryWrapper sourceDirectory = GetDirectory(@"a:\source\");

            DirectoryWrapper destinationSub1 = GetDirectory(@"b:\target\sub", filePaths: new[] { "b.js" });
            DirectoryWrapper destinationSub2 = GetDirectory(@"b:\target\sub3", filePaths: new[] { "o.js" });
            DirectoryWrapper destinationDirectory = GetDirectory(@"b:\target\", directories: new[] { destinationSub1, destinationSub2 });

            FileSystemHelpers.SmartCopy(@"a:\source\",
                                        @"b:\target\",
                                         previousPaths.Contains,
                                         sourceDirectory.Directory,
                                         destinationDirectory.Directory,
                                         path => GetDirectory(path).Directory,
                                         skipOldFiles: false);

            destinationSub1.VerifyDeleted();
            destinationSub2.VerifyNotDeleted();
        }

        private static DirectoryWrapper GetDirectory(string fullName,
                                                      IEnumerable<string> filePaths = null,
                                                      IEnumerable<DirectoryWrapper> directories = null,
                                                      bool hidden = false,
                                                      bool exists = true)
        {
            filePaths = filePaths ?? Enumerable.Empty<string>();
            directories = directories ?? Enumerable.Empty<DirectoryWrapper>();

            var files = filePaths.Select(path =>
            {
                var mock = new Mock<FileInfoBase>();
                bool fileExists = true;
                mock.Setup(m => m.Exists).Returns(() => fileExists);
                mock.Setup(m => m.FullName).Returns(Path.Combine(fullName, path));
                mock.Setup(m => m.Name).Returns(path);
                mock.Setup(m => m.Delete()).Callback(() => fileExists = false);
                return mock;
            })
            .ToDictionary(f => f.Object.Name, StringComparer.OrdinalIgnoreCase);

            var directoryMapping = directories.ToDictionary(d => GetShortName(fullName, d.Directory.FullName), StringComparer.OrdinalIgnoreCase);

            var mockDir = new Mock<DirectoryInfoBase>();
            mockDir.Setup(m => m.FullName).Returns(fullName);
            mockDir.Setup(m => m.Name).Returns(fullName.Replace(Path.GetDirectoryName(fullName) + @"\", ""));
            mockDir.Setup(m => m.GetFiles()).Returns(files.Values.Select(f => f.Object).ToArray());
            mockDir.Setup(m => m.GetDirectories()).Returns(directoryMapping.Values.Select(d => d.Directory).ToArray());
            mockDir.Setup(m => m.Exists).Returns(exists);

            if (hidden)
            {
                mockDir.Setup(m => m.Attributes).Returns(FileAttributes.Hidden);
            }

            return new DirectoryWrapper
            {
                MockDirectory = mockDir,
                Files = files,
                Directories = directoryMapping
            };
        }

        private static string GetShortName(string prefix, string path)
        {
            return path.Substring(prefix.Length).Trim(Path.DirectorySeparatorChar);
        }

        private class DirectoryWrapper
        {
            public DirectoryInfoBase Directory
            {
                get
                {
                    return MockDirectory.Object;
                }
            }

            public Mock<DirectoryInfoBase> MockDirectory { get; set; }
            public IDictionary<string, Mock<FileInfoBase>> Files { get; set; }
            public IDictionary<string, DirectoryWrapper> Directories { get; set; }

            public void VerifyCopied(string path, string toPath)
            {
                Assert.True(Files.ContainsKey(path));
                Files[path].Verify(m => m.CopyTo(toPath, true), Times.Once());
            }

            public void VerifyNotCopied(string path, string toPath = null)
            {
                Assert.True(Files.ContainsKey(path));
                if (String.IsNullOrEmpty(toPath))
                {
                    Files[path].Verify(m => m.CopyTo(It.IsAny<string>(), true), Times.Never());
                }
                else
                {
                    Files[path].Verify(m => m.CopyTo(toPath, true), Times.Never());
                }
            }

            public void VerifyDeleted()
            {
                MockDirectory.Verify(m => m.Delete(true), Times.Once());
            }

            public void VerifyDeleted(string path)
            {
                Assert.True(Files.ContainsKey(path));
                Files[path].Verify(m => m.Delete(), Times.Once());
            }

            public void VerifyNotDeleted()
            {
                MockDirectory.Verify(m => m.Delete(true), Times.Never());
            }

            public void VerifyNotDeleted(string path)
            {
                Assert.True(Files.ContainsKey(path));
                Files[path].Verify(m => m.Delete(), Times.Never());
            }

            public void VerifyCreated()
            {
                MockDirectory.Verify(m => m.Create(), Times.Once());
            }

            public void VerifyCreated(string path)
            {
                Assert.True(Directories.ContainsKey(path));
                Directories[path].VerifyCreated();
            }
        }
    }
}
