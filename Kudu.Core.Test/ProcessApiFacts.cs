using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Services.Performance;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using System.Web.Http;

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
            var fileInfoFactory = new Mock<IFileInfoFactory>();
            var fileInfo = new Mock<FileInfoBase>();

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(file.Object);
            fileSystem.SetupGet(fs => fs.FileInfo)
                      .Returns(fileInfoFactory.Object);
            file.Setup(f => f.Exists(path))
                      .Returns(true);
            file.Setup(f => f.OpenRead(path))
                      .Returns(() => new MemoryStream());
            fileInfoFactory.Setup(f => f.FromFileName(path))
                           .Returns(() => fileInfo.Object);
            fileInfo.Setup(f => f.Exists)
                    .Returns(true);
            FileSystemHelpers.Instance = fileSystem.Object;

            // Test
            using (var stream = ProcessController.FileStreamWrapper.OpenRead(path))
            {
            }

            // Assert
            fileInfo.Verify(f => f.Delete(), Times.Once());
        }

        [Fact]
        public void MiniDumpDeleteOnCloseTests()
        {
            // Arrange
            var path = @"x:\temp\minidump.dmp";
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();
            var fileInfoFactory = new Mock<IFileInfoFactory>();
            var fileInfo = new Mock<FileInfoBase>();

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(file.Object);
            fileSystem.SetupGet(fs => fs.FileInfo)
                      .Returns(fileInfoFactory.Object);
            file.Setup(f => f.Exists(path))
                      .Returns(true);
            file.Setup(f => f.OpenRead(path))
                      .Returns(() => new MemoryStream());
            fileInfoFactory.Setup(f => f.FromFileName(path))
                           .Returns(() => fileInfo.Object);
            fileInfo.Setup(f => f.Exists)
                    .Returns(true);
            FileSystemHelpers.Instance = fileSystem.Object;

            // Test
            var stream = ProcessController.FileStreamWrapper.OpenRead(path);
            stream.Close();

            // Assert
            fileInfo.Verify(f => f.Delete(), Times.Once());
        }

        [Fact]
        public void MiniDumpFreeModeTests()
        {
            var settings = new Mock<IDeploymentSettingsManager>();
            var controller = new Mock<ProcessController>(Mock.Of<ITracer>(), null, settings.Object);

            // Setup
            controller.Object.Request = new HttpRequestMessage();
            settings.Setup(s => s.GetValue(SettingsKeys.WebSiteSku, It.IsAny<bool>()))
                    .Returns("Free");

            // Test
            var response = controller.Object.MiniDump(0, 2);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            var error = response.Content.ReadAsAsync<JObject>().Result;
            Assert.Equal("Site sku (Free) does not support full minidump.", error.Value<string>("Message"));
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

        [Fact]
        public async Task ProcesStartBasicTests()
        {
            var tracer = Mock.Of<ITracer>();
            var process = new Mock<IProcess>();
            var idleManager = new IdleManager(TimeSpan.MaxValue, tracer);

            var expectedExitCode = 10;
            var input = "this is input";
            var output = "this is output";
            var error = "this is error";
            var inputBuffer = new byte[1024];

            var actualOutput = new MemoryStream();
            var actualError = new MemoryStream();
            var actualInput = new MemoryStream(inputBuffer);

            var expectedInput = new MemoryStream();
            var bytes = Encoding.UTF8.GetBytes(input);
            expectedInput.Write(bytes, 0, bytes.Length);
            expectedInput.Position = 0;

            var expectedOutput = new MemoryStream();
            bytes = Encoding.UTF8.GetBytes(output);
            expectedOutput.Write(bytes, 0, bytes.Length);
            expectedOutput.Position = 0;

            var expectedError = new MemoryStream();
            bytes = Encoding.UTF8.GetBytes(error);
            expectedError.Write(bytes, 0, bytes.Length);
            expectedError.Position = 0;

            // Setup
            process.SetupGet(p => p.StandardInput)
                   .Returns(new StreamWriter(actualInput));
            process.SetupGet(p => p.StandardOutput)
                   .Returns(new StreamReader(expectedOutput));
            process.SetupGet(p => p.StandardError)
                   .Returns(new StreamReader(expectedError));
            process.SetupGet(p => p.ExitCode)
                   .Returns(expectedExitCode);
            process.Setup(p => p.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(true);

            // Test
            int actualExitCode = await process.Object.Start(tracer, actualOutput, actualError, expectedInput, idleManager);

            // Assert
            Assert.Equal(expectedExitCode, actualExitCode);
            Assert.Throws<ObjectDisposedException>(() => actualInput.Length);
            Assert.Equal(input, Encoding.UTF8.GetString(inputBuffer, 0, input.Length));
            Assert.Equal(output, Encoding.UTF8.GetString(actualOutput.GetBuffer(), 0, (int)actualOutput.Length));
            Assert.Equal(error, Encoding.UTF8.GetString(actualError.GetBuffer(), 0, (int)actualError.Length));
        }

        [Fact]
        public async Task ProcesOutputBlockedTests()
        {
            var tracer = Mock.Of<ITracer>();
            var process = new Mock<IProcess>();
            var idleManager = new IdleManager(TimeSpan.MaxValue, tracer);
            var output = new Mock<Stream>(MockBehavior.Strict);
            CancellationToken cancellationToken;

            // Setup
            output.SetupGet(o => o.CanRead)
                  .Returns(true);
            output.Setup(o => o.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .Callback((byte[] buffer, int offset, int count, CancellationToken token) => { cancellationToken = token; })
                  .Returns(async () =>
                  {
                      await Task.Delay(5000, cancellationToken);
                      return 0;
                  });
            process.SetupGet(p => p.StandardInput)
                   .Returns(new StreamWriter(new MemoryStream()));
            process.SetupGet(p => p.StandardOutput)
                   .Returns(new StreamReader(output.Object));
            process.SetupGet(p => p.StandardError)
                   .Returns(new StreamReader(new MemoryStream()));
            process.Setup(p => p.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(true);

            var timeout = ProcessExtensions.StandardOutputDrainTimeout;
            try
            {
                // Speedup the test
                ProcessExtensions.StandardOutputDrainTimeout = TimeSpan.FromSeconds(1);

                // Test
                await process.Object.Start(tracer, new MemoryStream(), new MemoryStream(), null, idleManager);

                throw new InvalidOperationException("Should not reach here!");
            }
            catch (TimeoutException)
            {
                // No-op
            }
            finally
            {
                ProcessExtensions.StandardOutputDrainTimeout = timeout;
            }
        }

        [Fact]
        public void GetProcessUserNameTests()
        {
            var identity = WindowsIdentity.GetCurrent();
            var process = Process.GetCurrentProcess();

            var userName = process.GetUserName();

            // Assert
            Assert.Equal(userName, identity.Name);
        }

        [Fact]
        public async Task StartProfileAsync_InvalidProcess()
        {
            var settings = new Mock<IDeploymentSettingsManager>();
            var controller = new Mock<ProcessController>(Mock.Of<ITracer>(), null, settings.Object);

            // Setup
            controller.Object.Request = new HttpRequestMessage();

            // Test
            var ex = await Assert.ThrowsAsync<HttpResponseException>(() => controller.Object.StartProfileAsync(100));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
        }

        [Fact]
        public async Task StopProfileAsync_InvalidProcess()
        {
            var settings = new Mock<IDeploymentSettingsManager>();
            var controller = new Mock<ProcessController>(Mock.Of<ITracer>(), null, settings.Object);

            // Setup
            controller.Object.Request = new HttpRequestMessage();

            // Test
            var ex = await Assert.ThrowsAsync<HttpResponseException>(() => controller.Object.StopProfileAsync(100));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
        }

    }
}
