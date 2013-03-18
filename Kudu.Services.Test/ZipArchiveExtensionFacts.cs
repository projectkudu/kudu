using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Services.Test
{
    public class ZipArchiveExtensionFacts
    {
        [Theory]
        [InlineData("Foo.txt", "This was a triumph")]
        [InlineData(@"Bar\Qux\Foo.txt", "I'm making a note here")]
        public void AddFileToArchiveCreatesEntryUnderDirectory(string fileName, string content)
        {
            // Arrange
            var stream = new MemoryStream();
            var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            var fileInfo = CreateFile(fileName, content);

            // Act
            zip.AddFile(fileInfo, "");


            // Assert
            zip.Dispose();
            zip = new ZipArchive(ReOpen(stream));
            Assert.Equal(1, zip.Entries.Count);
            AssertZipEntry(zip, fileName, content);
        }

        [Fact]
        public void AddDirectoryToArchiveIncludesDirectoryTreeInArchive()
        {
            // Arrange
            var stream = new MemoryStream();
            var zip = new ZipArchive(stream, ZipArchiveMode.Create);

            
            var emptyDir = new Mock<DirectoryInfoBase>();
            emptyDir.SetupGet(d => d.Name).Returns("empty-dir");
            emptyDir.Setup(d => d.GetFileSystemInfos()).Returns(new FileSystemInfoBase[0]);
            var subDir = new Mock<DirectoryInfoBase>();
            subDir.SetupGet(d => d.Name).Returns("site");
            subDir.Setup(d => d.GetFileSystemInfos()).Returns(new FileSystemInfoBase[] { emptyDir.Object, CreateFile("home.aspx", "home content"), CreateFile("site.css", "some css") });
            

            var directoryInfo = new Mock<DirectoryInfoBase>();
            directoryInfo.SetupGet(f => f.Name).Returns("zip-test");
            directoryInfo.Setup(f => f.GetFileSystemInfos()).Returns(new FileSystemInfoBase[] { subDir.Object, CreateFile("zero-length-file", ""), CreateFile("log.txt", "log content") });

            // Act
            zip.AddDirectory(directoryInfo.Object, "");

            // Assert
            zip.Dispose();
            zip = new ZipArchive(ReOpen(stream));
            Assert.Equal(5, zip.Entries.Count);
            AssertZipEntry(zip, "log.txt", "log content");
            AssertZipEntry(zip, @"site\home.aspx", "home content");
            AssertZipEntry(zip, @"site\site.css", "some css");
            AssertZipEntry(zip, @"site\empty-dir\", null);
            AssertZipEntry(zip, @"zero-length-file", null);
        }

        private static void AssertZipEntry(ZipArchive archive, string fileName, string content)
        {
            ZipArchiveEntry entry = archive.GetEntry(fileName);
            Assert.NotNull(entry);
            using (var streamReader = new StreamReader(entry.Open()))
            {
                Assert.Equal(content, streamReader.ReadLine());
            }
        }

        private static Stream GetStream(string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            return stream;
        }

        private static MemoryStream ReOpen(MemoryStream memoryStream)
        {
            return new MemoryStream(memoryStream.ToArray());
        }

        private static FileInfoBase CreateFile(string fileName, string content)
        {
            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.SetupGet(f => f.Name).Returns(fileName);
            fileInfo.Setup(f => f.OpenRead()).Returns(GetStream(content));
            return fileInfo.Object;
        }
    }
}
