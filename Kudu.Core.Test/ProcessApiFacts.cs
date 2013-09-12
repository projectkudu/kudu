using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Services.Performance;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Core.Test
{
    public class ProcessApiFacts
    {
        [Fact]
        public void MiniDumpDeleteOnDisposeTests()
        {
            // Arrange
            var path = @"x:\temp\minidump.dmp";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(file.Object);
            file.Setup(f => f.Exists(path))
                      .Returns(true);
            file.Setup(f => f.OpenRead(path))
                      .Returns(() => new MemoryStream());

            // Test
            using (var stream = ProcessController.FileStreamWrapper.OpenRead(path, fileSystem.Object))
            {
            }

            // Assert
            file.Verify(f => f.Delete(path), Times.Once());
        }

        [Fact]
        public void MiniDumpDeleteOnCloseTests()
        {
            // Arrange
            var path = @"x:\temp\minidump.dmp";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(file.Object);
            file.Setup(f => f.Exists(path))
                      .Returns(true);
            file.Setup(f => f.OpenRead(path))
                      .Returns(() => new MemoryStream());

            // Test
            var stream = ProcessController.FileStreamWrapper.OpenRead(path, fileSystem.Object);
            stream.Close();

            // Assert
            file.Verify(f => f.Delete(path), Times.Once());
        }

        [Fact]
        public void MiniDumpFreeModeTests()
        {
            var settings = new Mock<IDeploymentSettingsManager>();
            var controller = new Mock<ProcessController>(Mock.Of<ITracer>(), null, settings.Object, null);

            // Setup
            controller.Object.Request = new HttpRequestMessage();
            settings.Setup(s => s.GetValue(SettingsKeys.WebSiteComputeMode, It.IsAny<bool>()))
                    .Returns("Shared");
            settings.Setup(s => s.GetValue(SettingsKeys.WebSiteSiteMode, It.IsAny<bool>()))
                    .Returns("Limited");

            // Test
            var response = controller.Object.MiniDump(0, 2);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            var error = response.Content.ReadAsAsync<JObject>().Result;
            Assert.Equal("Site mode (Shared|Limited) does not support full minidump.", error.Value<string>("Message"));
        }

        [Fact]
        public void GetParentProcessTests()
        {
            var prompt = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"SET /P __AREYOUSURE=Are you sure (Y/[N])?\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            try
            {
                // Test
                var parent = prompt.GetParentProcess(Mock.Of<ITracer>());

                // Assert
                Assert.Equal(Process.GetCurrentProcess().ProcessName, parent.ProcessName);
                Assert.Equal(Process.GetCurrentProcess().Id, parent.Id);
            }
            finally
            {
                prompt.Kill();
            }
        }
    }
}
