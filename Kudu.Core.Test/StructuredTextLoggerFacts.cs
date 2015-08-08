using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Moq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Xunit;

namespace Kudu.Core.Test
{
    public class StructuredTextLoggerFacts
    {
        [Theory]
        [InlineData("This message is clean from any illegal characters", LogEntryType.Message)]
        [InlineData("This message has \"double quotes\" in it.", LogEntryType.Error)]
        [InlineData("This message has, commas, in, it.", LogEntryType.Warning)]
        public void StructuredTextLoggerLogFunction(string message, LogEntryType type)
        {
            //Arrange
            var analytics = new Mock<IAnalytics>();
            var logger = new StructuredTextLogger(@"x:\log.log", analytics.Object);
            var fileSystemInfo = GetTextLoggerMockFileSystem();
            FileSystemHelpers.Instance = fileSystemInfo.FileSystem;

            //Act
            logger.Log(message, type);
            var entries = logger.GetLogEntries();

            //Assert
            Assert.Equal(1, entries.Count());
            Assert.Equal(message, entries.First().Message);
            Assert.Equal(type, entries.First().Type);

            //Clean up
            fileSystemInfo.MemoryStream.RealDispose();
        }

        [Fact]
        public void StructuredTextLoggerUsesHighestChildTypeToUpdateParentType()
        {
            //Arrange
            var analytics = new Mock<IAnalytics>();
            var logger = new StructuredTextLogger(@"x:\log.log", analytics.Object);
            var fileSystemInfo = GetTextLoggerMockFileSystem();
            const string message = "Message 1";
            const string error = "Error 1";
            FileSystemHelpers.Instance = fileSystemInfo.FileSystem;

            //Act
            var sublogger = logger.Log(message, LogEntryType.Message);
            sublogger.Log(error, LogEntryType.Error);
            var entries = logger.GetLogEntries();

            //Assert
            Assert.Equal(1, entries.Count());
            Assert.Equal(message, entries.First().Message);
            Assert.Equal(LogEntryType.Error, entries.First().Type);

            var subEntries = logger.GetLogEntryDetails(entries.First().Id);

            Assert.Equal(1, subEntries.Count());
            Assert.Equal(error, subEntries.First().Message);
            Assert.Equal(LogEntryType.Error, subEntries.First().Type);

            //Clean up
            fileSystemInfo.MemoryStream.RealDispose();
        }

        internal static FileSystemInfo GetTextLoggerMockFileSystem()
        {
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var stream = new TestMemoryStream();

            fileSystem.Setup(f => f.File).Returns(fileBase.Object);

            fileBase.Setup(f => f.Open(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
                    .Returns(stream);
            fileBase.Setup(f => f.OpenRead(It.IsAny<string>()))
                    .Returns((string path) => 
                    {
                        stream.Position = 0;
                        return stream;
                    });
            fileBase.Setup(f => f.ReadAllText(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        using (var reader = new StreamReader(fileBase.Object.OpenRead(path)))
                        {
                            return reader.ReadToEnd();
                        }
                    });
            fileBase.Setup(f => f.ReadAllLines(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        return fileBase.Object.ReadAllText(path).Split(new[] { System.Environment.NewLine }, StringSplitOptions.None);
                    });

            return new FileSystemInfo
            {
                FileSystem = fileSystem.Object,
                MemoryStream = stream
            };
        }

        internal class FileSystemInfo
        {
            public IFileSystem FileSystem { get; set; }
            public TestMemoryStream MemoryStream { get; set; }
        }

        internal class TestMemoryStream : MemoryStream
        {
            public void RealDispose()
            {
                base.Dispose();
            }

            [SuppressMessage("Microsoft.Usage", "CA2215", Justification = "We need the stream to still be readable after using statement")] 
            protected override void Dispose(bool disposing)
            {
                //no-op for testing
            }
        }
    }
}
