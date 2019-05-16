using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.Core.Test.Settings
{
    public class DiagnosticsSettingsManagerTests
    {
        private readonly byte[] _buffer = new byte[1024];

        [Fact]
        public void BasicReadWriteTests()
        {
            BuildFileSystemMock((Mock<IFileSystem> fileSystemMock) =>
            {
                FileSystemHelpers.Instance = fileSystemMock.Object;
                var mockTracer = new Mock<ITracer>();

                var manager = new DiagnosticsSettingsManager(@"x:\test.json", mockTracer.Object);

                // should see default value
                DiagnosticsSettings settings = manager.GetSettings();
                Assert.Equal(false, settings.AzureDriveEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureDriveTraceLevel);
                Assert.Equal(false, settings.AzureTableEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureTableTraceLevel);
                Assert.Equal(false, settings.AzureBlobEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureBlobTraceLevel);

                // update with good value
                manager.UpdateSetting(DiagnosticsSettings.AzureDriveEnabledKey, true);
                manager.UpdateSetting(DiagnosticsSettings.AzureDriveTraceLevelKey, TraceEventType.Verbose);
                manager.UpdateSetting(DiagnosticsSettings.AzureTableEnabledKey, true);
                manager.UpdateSetting(DiagnosticsSettings.AzureTableTraceLevelKey, TraceEventType.Warning);
                manager.UpdateSetting(DiagnosticsSettings.AzureBlobEnabledKey, true);
                manager.UpdateSetting(DiagnosticsSettings.AzureBlobTraceLevelKey, TraceEventType.Information);

                settings = manager.GetSettings();
                Assert.Equal(true, settings.AzureDriveEnabled);
                Assert.Equal(TraceEventType.Verbose, settings.AzureDriveTraceLevel);
                Assert.Equal(true, settings.AzureTableEnabled);
                Assert.Equal(TraceEventType.Warning, settings.AzureTableTraceLevel);
                Assert.Equal(true, settings.AzureBlobEnabled);
                Assert.Equal(TraceEventType.Information, settings.AzureBlobTraceLevel);

                // number and string should be good too
                manager.UpdateSetting(DiagnosticsSettings.AzureDriveTraceLevelKey, 2);
                settings = manager.GetSettings();
                Assert.Equal(TraceEventType.Error, settings.AzureDriveTraceLevel);

                JsonSerializationException exception = null;

                try
                {
                    manager.UpdateSetting(DiagnosticsSettings.AzureTableTraceLevelKey, "Error");
                }
                catch (JsonSerializationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal("Error getting value from 'AzureTableTraceLevel' on 'Kudu.Contracts.Settings.DiagnosticsSettings'.", exception.Message);

                try
                {
                    manager.UpdateSetting(DiagnosticsSettings.AzureTableTraceLevelKey, "8");
                }
                catch (JsonSerializationException ex)
                {
                    exception = ex;
                }

                Assert.Equal("Error getting value from 'AzureTableTraceLevel' on 'Kudu.Contracts.Settings.DiagnosticsSettings'.", exception.Message);

                try
                {
                    manager.UpdateSetting(DiagnosticsSettings.AzureTableTraceLevelKey, "foo");
                }
                catch (JsonSerializationException ex)
                {
                    exception = ex;
                }

                Assert.Equal("Error getting value from 'AzureTableTraceLevel' on 'Kudu.Contracts.Settings.DiagnosticsSettings'.", exception.Message);

                try
                {
                    manager.UpdateSetting(DiagnosticsSettings.AzureBlobTraceLevelKey, 999);
                }
                catch (JsonSerializationException ex)
                {
                    exception = ex;
                }

                Assert.Equal(string.Format(CultureInfo.InvariantCulture, "Error converting value '{0}'", 999), exception.Message);

                settings = manager.GetSettings();
                Assert.Equal(true, settings.AzureDriveEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureDriveTraceLevel);
                Assert.Equal(true, settings.AzureTableEnabled);
                Assert.Equal(TraceEventType.Warning, settings.AzureTableTraceLevel);
                Assert.Equal(true, settings.AzureBlobEnabled);
                Assert.Equal(TraceEventType.Information, settings.AzureBlobTraceLevel);

                // with corrupted content, kudu will auto-correct content and reset to default value
                manager = new DiagnosticsSettingsManager(@"x:\bad-content.json", mockTracer.Object);
                settings = manager.GetSettings();
                Assert.Equal(false, settings.AzureDriveEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureDriveTraceLevel);
                Assert.Equal(false, settings.AzureTableEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureTableTraceLevel);
                Assert.Equal(false, settings.AzureBlobEnabled);
                Assert.Equal(TraceEventType.Error, settings.AzureBlobTraceLevel);
            });
        }

        private void BuildFileSystemMock(Action<Mock<IFileSystem>> action)
        {
            var fileSystemMock = new Mock<IFileSystem>();
            var fileBaseMock = new Mock<FileBase>();
            var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
            var fileInfoMock = new Mock<FileInfoBase>();
            var directoryBaseMock = new Mock<DirectoryBase>();
            var files = new Dictionary<string, TestMemoryStream>();

            fileSystemMock.SetupGet(fs => fs.File)
                      .Returns(fileBaseMock.Object);

            fileSystemMock.SetupGet(fs => fs.FileInfo)
                      .Returns(fileInfoFactoryMock.Object);

            fileInfoFactoryMock.Setup(f => f.FromFileName(It.IsAny<string>()))
                                .Returns(fileInfoMock.Object);

            fileSystemMock.SetupGet(fs => fs.Directory)
                      .Returns(directoryBaseMock.Object);

            fileBaseMock.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
            fileBaseMock.Setup(f => f.Open(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    .Returns((string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) =>
                    {
                        TestMemoryStream stream;
                        if (!files.TryGetValue(path, out stream))
                        {
                            stream = new TestMemoryStream();
                            byte[] contentInBytes = Encoding.UTF8.GetBytes("{}");
                            if (string.Equals(@"x:\bad-content.json", path))
                            {
                                contentInBytes = Encoding.UTF8.GetBytes(@"{
                                                                            ""AzureDriveEnabled"": true,
                                                                            ""AzureDriveTraceLevel"": 2,
                                                                            ""AzureTableEnabled"": true,
                                                                            ""AzureTableTraceLevel"": 8,
                                                                            ""AzureBlobEnabled"": true,
                                                                            ""AzureBlobTraceLevel"": 16
                                                                          }");
                            }
                            stream.WriteAsync(contentInBytes, 0, contentInBytes.Length);
                        }

                        stream.Position = 0;
                        return stream;
                    });

            fileBaseMock.Setup(f => f.Open(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                    .Returns((string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) =>
                    {
                        files[path] = new TestMemoryStream();
                        return files[path];
                    });

            directoryBaseMock.Setup(d => d.Exists(It.IsAny<string>())).Returns(true);

            try
            {
                action(fileSystemMock);
            }
            finally
            {
                foreach (var f in files)
                {
                    f.Value.RealDispose();
                }
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
