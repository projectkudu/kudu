using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class XmlLoggerFacts
    {
        [Fact]
        public void XmlLoggerFileNotExistTest()
        {
            var path = @"x:\deployments\1234\log.xml";
            var fileSystem = new Mock<IFileSystem>();

            // Setup
            fileSystem.SetupGet(f => f.File)
                      .Returns(Mock.Of<FileBase>());
            FileSystemHelpers.Instance = fileSystem.Object;

            // Test
            var logger = new XmlLogger(path, Mock.Of<IAnalytics>());
            var entries = logger.GetLogEntries();
            
            // Assert
            Assert.Equal(0, entries.Count());
        }

        [Fact]
        public void XmlLoggerBasicTest()
        {
            var path = @"x:\deployments\1234\log.xml";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();
            var id = Guid.NewGuid().ToString();
            var message = Guid.NewGuid().ToString();
            var doc = new XDocument(new XElement("entries", 
                new XElement("entry",
                    new XAttribute("time", "2013-12-08T01:58:24.0247841Z"),
                    new XAttribute("id", id),
                    new XAttribute("type", "0"),
                    new XElement("message", message)
                )
            ));
            var mem = new MemoryStream();
            doc.Save(mem);

            // Setup
            fileSystem.SetupGet(f => f.File)
                      .Returns(file.Object);
            file.Setup(f => f.Exists(path))
                .Returns(true);
            file.Setup(f => f.OpenRead(path))
                .Returns(() => { mem.Position = 0; return mem; });
            FileSystemHelpers.Instance = fileSystem.Object;

            // Test
            var logger = new XmlLogger(path, Mock.Of<IAnalytics>());
            var entries = logger.GetLogEntries();

            // Assert
            Assert.Equal(1, entries.Count());
            Assert.Equal(id, entries.First().Id);
            Assert.Equal(message, entries.First().Message);
        }

        [Fact]
        public void XmlLoggerMalformedXmlTest()
        {
            var path = @"x:\deployments\1234\log.xml";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();
            var analytics = new Mock<IAnalytics>();
            var bytes = Encoding.UTF8.GetBytes("<invalid xml");
            var mem = new MemoryStream();
            mem.Write(bytes, 0, bytes.Length);

            // Setup
            fileSystem.SetupGet(f => f.File)
                      .Returns(file.Object);
            file.Setup(f => f.Exists(path))
                .Returns(true);
            file.Setup(f => f.OpenRead(path))
                .Returns(() => { mem.Position = 0; return mem; });
            FileSystemHelpers.Instance = fileSystem.Object;

            // Test
            var logger = new XmlLogger(path, analytics.Object);
            var entries = logger.GetLogEntries();

            // Assert
            Assert.Equal(0, entries.Count());
            analytics.Verify(a => a.UnexpectedException(It.IsAny<Exception>(), false), Times.Once);
        }
    }
}