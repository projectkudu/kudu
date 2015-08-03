using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Moq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Xunit;

namespace Kudu.Core.Test
{
    public class StructuredTextDocumentFacts
    {
        public T Id<T>(T obj)
        {
            return obj;
        }

        [Theory]
        [InlineData("\tInvalid log entry that starts with \\t")]
        [InlineData("Invalid log entry \n that contains a \\n")]
        public void StructuredDocumentThrowsAFormatExceptionForInvalidLogEntries(string logEntry)
        {
            //Arrange
            Exception exception = null;
            var structuredDocument = new StructuredTextDocument<string>(@"x:\deployment\log.log", Id, Id);

            //Act
            try
            {
                structuredDocument.Write(logEntry, 0);
            }
            catch (Exception e)
            {
                exception = e;
            }

            //Assert
            Assert.IsType(typeof(FormatException), exception);
            Assert.Contains("Serialized log of type", exception.Message);
        }

        [Theory]
        [InlineData("log entry 1", 0)]
        [InlineData("log entry 2", 5)]
        [InlineData("log entry 3", 11)]
        public void StructuredDocumentLogsWithCorrectDepth(string logEntry, int depth)
        {
            //Arrange
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var stream = new TestMemoryStream();
            var structuredDocument = new StructuredTextDocument<string>(@"x:\log.log", Id, Id);
            fileSystem.Setup(f => f.File).Returns(fileBase.Object);

            fileBase.Setup(f => f.Open(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
                    .Returns(stream);
            FileSystemHelpers.Instance = fileSystem.Object;

            //Act
            structuredDocument.Write(logEntry, depth);

            //Assert
            Assert.True(stream.Position > 0, "must write data");
            stream.Position = 0;
            var result = new StreamReader(stream).ReadToEnd();
            var writtenDepth = StructuredTextDocument.GetEntryDepth(result);
            Assert.Equal(depth, writtenDepth);
            Assert.Equal(string.Concat(logEntry, System.Environment.NewLine), result.Substring(writtenDepth));
            stream.RealDispose();
        }

        [Fact]
        public void StructuredDocumentParsingTest()
        {
            //Arrange
            FileSystemHelpers.Instance = MockFileSystem.GetMockFileSystem(@"x:\log.log", () => "Message 1\r\nmessage 2\r\nmessage 3\r\n\tsub message 3.1\r\n\tsub message 3.2\r\n\tsub message 3.3\r\nmessage 4\r\n\tsub message 4.1");
            var ExpectedContent = new int[] { 0, 0, 3 , 1 };
            var structuredDocument = new StructuredTextDocument<string>(@"x:\log.log", Id, Id);

            //Act
            var parsed = structuredDocument.GetDocument().ToArray();

            //Assert
            Assert.Equal(4, parsed.Length);
            for (var i = 0; i < parsed.Count(); i++)
            {
                Assert.Equal(ExpectedContent[i], parsed[i].Children.Count());
            }
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
