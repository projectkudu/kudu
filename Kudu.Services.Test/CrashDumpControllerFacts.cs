using Kudu.Contracts.Diagnostics;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Services.Diagnostics;
using Kudu.Services.Performance;
using Moq;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kudu.Services.Test
{
    public class CrashDumpControllerFacts
    {
        [Fact]
        public void GetCrashDumpInfoFromFileInfoByName()
        {
            const string name = "test-name.dmp";
            const string fullName = "X:\\dumps\\test-name.dmp";
            const string baseUrl = "http://localhost/";
            var timestamp = DateTime.Now;

            var tracerMock = new Mock<ITracer>();
            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.CrashDumpsPath).Returns("X:\\dumps");

            var deploymentSettingsManagerMock = new Mock<IDeploymentSettingsManager>();
            using (var controller = new CrashDumpController(tracerMock.Object, environmentMock.Object, deploymentSettingsManagerMock.Object))
            {
                var fileSystemMock = new Mock<IFileSystem>();
                var fileBaseMock = new Mock<FileBase>();
                var fileInfoBaseMock = new Mock<FileInfoBase>();
                var fileInfoFactoryMock = new Mock<IFileInfoFactory>();

                fileSystemMock.Setup(f => f.FileInfo).Returns(fileInfoFactoryMock.Object);
                fileSystemMock.Setup(f => f.File).Returns(fileBaseMock.Object);

                fileInfoBaseMock.Setup(f => f.Name).Returns(name);
                fileInfoBaseMock.Setup(f => f.LastWriteTime).Returns(timestamp);
                fileInfoBaseMock.Setup(f => f.FullName).Returns(fullName);

                fileInfoFactoryMock.Setup(f => f.FromFileName(It.IsAny<string>()))
                    .Returns(fileInfoBaseMock.Object);

                fileBaseMock.Setup(f => f.Exists(It.IsAny<string>()))
                    .Returns((string path) => path == fullName);

                FileSystemHelpers.Instance = fileSystemMock.Object;

                controller.Request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUrl));

                var crashInfoDump = controller.GetCrashDump(name);

                Assert.Equal(name, crashInfoDump.Name);
                Assert.Equal(fullName, crashInfoDump.FilePath);
                Assert.Equal(timestamp, crashInfoDump.Timestamp);
                Assert.Equal($"{baseUrl}api/crashdumps/{name}", crashInfoDump.Href.ToString());
                Assert.Equal($"{baseUrl}api/crashdumps/{name}/analyze", crashInfoDump.AnalyizeHref.ToString());
                Assert.Equal($"{baseUrl}api/vfs/data/{Constants.Dumps}/{name}", crashInfoDump.DownloadHref.ToString());
            }
        }
    }
}
