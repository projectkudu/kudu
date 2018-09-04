using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Xunit;

namespace Kudu.Core.Test.Deployment
{
    public class DeploymentHelperFacts
    {
        [Fact]
        public void PurgeZipsIfNecessaryTest()
        {
            // Setup
            var tracer = new Mock<ITracer>();
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var directoryBase = new Mock<DirectoryBase>();

            Dictionary<string, DateTime> testFiles = new Dictionary<string, DateTime>()
            {
                {"testFile1.zip", new DateTime(year: 2018, month: 08, day: 21, hour: 12, minute: 31, second: 34)},
                {"testFile2.zip", new DateTime(year: 2018, month: 08, day: 21, hour: 12, minute: 31, second: 20)},
                {"testFile3.zip", new DateTime(year: 2018, month: 07, day: 30, hour: 7, minute: 21, second: 22)},
                {"testFile4.zip", new DateTime(year: 2018, month: 08, day: 25, hour: 0, minute: 0, second: 0)}
            };

            List<string> deletedFiles = new List<string>();

            directoryBase
                .Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(testFiles.Keys.ToArray());

            fileBase
                .Setup(f => f.GetLastWriteTimeUtc(It.IsAny<string>()))
                .Returns((string fileName) => testFiles[fileName]);

            fileBase
                .Setup(f => f.Delete(It.IsAny<string>()))
                .Callback((string path) => deletedFiles.Add(Path.GetFileName(path)));

            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directoryBase.Object);

            // Act
            FileSystemHelpers.Instance = fileSystem.Object;
            DeploymentHelper.PurgeZipsIfNecessary("testLocation", tracer.Object, 2);

            // Assert Set 1
            Assert.Contains("testFile3.zip", deletedFiles);
            Assert.Contains("testFile2.zip", deletedFiles);
            Assert.Equal(deletedFiles.Count, 2);

            // Reset and Act
            deletedFiles = new List<string>();
            DeploymentHelper.PurgeZipsIfNecessary("testLocation", tracer.Object, 5);
        
            // Assert Set 2
            Assert.Equal(deletedFiles.Count, 0);

            // Reset and Act
            deletedFiles = new List<string>();
            DeploymentHelper.PurgeZipsIfNecessary("testLocation", tracer.Object, 1);

            // Assert Set 3
            Assert.Contains("testFile3.zip", deletedFiles);
            Assert.Contains("testFile2.zip", deletedFiles);
            Assert.Contains("testFile1.zip", deletedFiles);
            Assert.Equal(deletedFiles.Count, 3);
        }
    }
}
