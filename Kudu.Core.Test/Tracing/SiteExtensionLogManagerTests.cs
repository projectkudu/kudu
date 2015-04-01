using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Sdk;

namespace Kudu.Core.Test.Tracing
{
    public class SiteExtensionLogManagerTests
    {
        private const string DirectoryPath = "directoryPath";

        private readonly SiteExtensionLogManager _siteExtensionLogManager;
        private readonly byte[] _buffer = new byte[1024];
        private readonly List<FileInfoBase> _mockLogFiles = new List<FileInfoBase>();
        private readonly List<IDictionary<string, object>> _logEvents = new List<IDictionary<string, object>>();
        private bool _deleteCalled;

        public SiteExtensionLogManagerTests()
        {
            _siteExtensionLogManager = BuildSiteExtensionLogManager();
        }

        [Fact]
        public void SiteExtensionLogManagerShouldLogOneEventToFile()
        {
            LogEvent("val1", 2);

            ValidateLogFileContent();
        }

        [Fact]
        public void SiteExtensionLogManagerShouldLogSeveralEventsToFile()
        {
            LogEvent("val1", 2);
            LogEvent("val2", 3);
            LogEvent("val3", 4);
            LogEvent("val4", 5);

            ValidateLogFileContent();
        }

        [Fact]
        public void SiteExtensionLogManagerShouldCleanupLogIfNeeded()
        {
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, -2, true);
            AddFileInfoBaseToFilesList(1024 * 1024, 2, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 4, false);

            LogEvent("val1", 2);

            Assert.True(_deleteCalled, "Delete should have been called");

            ValidateLogFileContent();
        }

        [Fact]
        public void SiteExtensionLogManagerShouldNotCleanupLogIfNotNeeded()
        {
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 2, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 1, false);
            AddFileInfoBaseToFilesList(1024 * 1024, 4, false);

            LogEvent("val1", 2);

            Assert.False(_deleteCalled, "Delete should not have been called");

            ValidateLogFileContent();
        }

        private void AddFileInfoBaseToFilesList(long length, int addHours, bool expectDelete)
        {
            var fileInfoBaseMock = new Mock<FileInfoBase>();

            fileInfoBaseMock.SetupGet(fs => fs.Length)
                            .Returns(length);

            fileInfoBaseMock.SetupGet(fs => fs.LastWriteTimeUtc)
                            .Returns(DateTime.Now.AddHours(addHours));

            fileInfoBaseMock.Setup(f => f.Delete())
                            .Callback(() =>
                            {
                                if (!expectDelete)
                                {
                                    throw new InvalidOperationException("Delete not expected");
                                }

                                _deleteCalled = true;
                            });

            _mockLogFiles.Add(fileInfoBaseMock.Object);
        }

        private void LogEvent(string val1, Int64 val2)
        {
            IDictionary<string, object> logEvent = new Dictionary<string, object>();
            logEvent["key1"] = val1;
            logEvent["key2"] = val2;

            _siteExtensionLogManager.Log(logEvent);

            _logEvents.Add(logEvent);
        }

        private void ValidateLogFileContent()
        {
            using (var reader = new StreamReader(new MemoryStream(_buffer)))
            {
                var logFileContent = reader.ReadToEnd();
                var lines = logFileContent.Split('\n');

                Assert.Equal(_logEvents.Count, lines.Length - 1);

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    var logLine = JsonConvert.DeserializeObject<Dictionary<string, object>>(lines[i]);

                    foreach (var property in _logEvents[i])
                    {
                        Assert.Equal(property.Value, logLine[property.Key]);
                    }
                }
            }
        }

        private Mock<IFileSystem> BuildFileSystemMock()
        {
            var fileSystemMock = new Mock<IFileSystem>();
            var fileBaseMock = new Mock<FileBase>();
            var directoryInfoFactoryMock = new Mock<IDirectoryInfoFactory>();
            var directoryInfoMock = new Mock<DirectoryInfoBase>();

            fileSystemMock.SetupGet(fs => fs.File)
                      .Returns(fileBaseMock.Object);

            fileSystemMock.SetupGet(fs => fs.DirectoryInfo)
                      .Returns(directoryInfoFactoryMock.Object);

            fileBaseMock.Setup(f => f.Open(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
                        .Returns(() =>
                        {
                            int index = _buffer.Length;
                            for (int i = 0; i < _buffer.Length; i++)
                            {
                                if (_buffer[i] == 0)
                                {
                                    index = i;
                                    break;
                                }
                            }
                            return new MemoryStream(_buffer, index, _buffer.Length - index);
                        });

            directoryInfoFactoryMock.Setup(d => d.FromDirectoryName(It.IsAny<string>()))
                        .Returns(directoryInfoMock.Object);

            directoryInfoFactoryMock.Setup(d => d.FromDirectoryName(It.IsAny<string>()))
                        .Returns(directoryInfoMock.Object);

            directoryInfoMock.Setup(d => d.GetFiles(It.IsAny<string>()))
                             .Returns(() => _mockLogFiles.ToArray());

            FileSystemHelpers.Instance = fileSystemMock.Object;

            return fileSystemMock;
        }

        private SiteExtensionLogManager BuildSiteExtensionLogManager()
        {
            FileSystemHelpers.Instance = BuildFileSystemMock().Object;
            var tracer = Mock.Of<ITracer>();

            return new SiteExtensionLogManager(tracer, DirectoryPath);
        }
    }
}