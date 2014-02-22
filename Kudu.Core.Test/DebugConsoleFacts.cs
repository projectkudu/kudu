using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Services;
using Microsoft.AspNet.SignalR;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class DebugConsoleFacts
    {
        [Fact]
        public async Task DebugConsoleOnConnectedTests()
        {
            // Setup
            var env = new Mock<IEnvironment>();
            var tracer = new Mock<ITracer>();
            var settings = new Mock<IDeploymentSettingsManager>();
            var connectionId = Guid.NewGuid().ToString();

            using (var controller = new PersistentCommandTest(env.Object, settings.Object, tracer.Object, Mock.Of<IProcess>()))
            {
                // Test
                await controller.Connect(Mock.Of<IRequest>(), connectionId);

                // Assert
                Assert.Equal(1, PersistentCommandTest.ProcessCount);

                // Test
                await controller.Disconnect(Mock.Of<IRequest>(), connectionId);

                // Assert
                Assert.Equal(0, PersistentCommandTest.ProcessCount);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DebugConsoleOnReceivedTests(bool connect)
        {
            // Setup
            var env = new Mock<IEnvironment>();
            var tracer = new Mock<ITracer>();
            var settings = new Mock<IDeploymentSettingsManager>();
            var process = new Mock<IProcess>();
            var connectionId = Guid.NewGuid().ToString();
            var data = Guid.NewGuid().ToString();
            var mem = new MemoryStream();

            using (var controller = new PersistentCommandTest(env.Object, settings.Object, tracer.Object, process.Object))
            {
                // Setup
                process.SetupGet(p => p.StandardInput)
                       .Returns(new StreamWriter(mem));

                // Test
                if (connect)
                {
                    await controller.Connect(Mock.Of<IRequest>(), connectionId);
                }

                await controller.Receive(Mock.Of<IRequest>(), connectionId, data);

                // Assert
                Assert.Equal(1, PersistentCommandTest.ProcessCount);

                if (connect)
                {
                    Assert.True(mem.Position > 0, "must write data");

                    mem.Position = 0;
                    var result = new StreamReader(mem).ReadToEnd();
                    Assert.True(result.EndsWith("\r\n"));
                    Assert.Equal(data, result.Substring(0, result.Length - 2));
                }
                else
                {
                    Assert.True(mem.Position == 0, "must skip data");
                }
            }
        }

        [Fact]
        public async Task DebugConsoleMaxProcessesTests()
        {
            var listOfControllers = new List<PersistentCommandTest>();

            try
            {
                // Setup
                var env = new Mock<IEnvironment>();
                var tracer = new Mock<ITracer>();
                var settings = new Mock<IDeploymentSettingsManager>();

                // Test
                for (int i = 0; i < 10; i++)
                {
                    var controller = new PersistentCommandTest(env.Object, settings.Object, tracer.Object, Mock.Of<IProcess>());
                    var connectionId = Guid.NewGuid().ToString();
                    await controller.Connect(Mock.Of<IRequest>(), connectionId);
                    listOfControllers.Add(controller);

                    // Assert
                    Assert.Equal(Math.Min(i + 1, PersistentCommandController.MaxProcesses), PersistentCommandTest.ProcessCount);
                }
            }
            finally
            {
                foreach (var controller in listOfControllers)
                {
                    controller.Dispose();
                }
            }
        }

        [Fact]
        public async Task DebugConsoleOnDisconnectedTests()
        {
            // Setup
            var env = new Mock<IEnvironment>();
            var tracer = new Mock<ITracer>();
            var settings = new Mock<IDeploymentSettingsManager>();
            var process = new Mock<IProcess>();
            var connectionId = Guid.NewGuid().ToString();

            process.Setup(p => p.Kill(tracer.Object))
                   .Verifiable();

            // Test
            using (var controller = new PersistentCommandTest(env.Object, settings.Object, tracer.Object, process.Object))
            {
                await controller.Connect(Mock.Of<IRequest>(), connectionId);

                // Assert
                Assert.Equal(1, PersistentCommandTest.ProcessCount);

                await controller.Disconnect(Mock.Of<IRequest>(), connectionId);
            }

            // Assert
            process.Verify();

            // Assert
            Assert.Equal(0, PersistentCommandTest.ProcessCount);
        }

        [Theory]
        [InlineData("pending", new [] { "pending" })]
        [InlineData("line\r\n", new[] { "line\r\n" })]
        [InlineData("line1\rline2\r\n", new[] { "line1\r", "line2\r\n" })]
        [InlineData("line\r\npending", new[] { "line\r\n", "pending" })]
        [InlineData("\r\npending", new[] { "\r\n", "pending" })]
        [InlineData("line\r", new[] { "line\r" })]
        [InlineData("\rpending", new[] { "\r", "pending" })]
        public async Task DebugConsoleReadLineAsyncTests(string actual, string[] expecteds)
        {
            using (StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(actual))))
            {
                int count = 0;
                PersistentCommandController.StreamResult line;
                StringBuilder strb = new StringBuilder();
                while ((line = await PersistentCommandController.ReadLineAsync(reader, strb)) != null)
                {
                    strb.Clear();
                    Assert.Equal(line.Value, expecteds[count]);
                    ++count;
                }

                Assert.Equal(expecteds.Length, count);
            }
        }

        public class PersistentCommandTest : PersistentCommandController, IDisposable
        {
            private readonly IProcess _process;

            public PersistentCommandTest(IEnvironment environment, IDeploymentSettingsManager settings, ITracer tracer, IProcess process)
                : base(environment, settings, tracer)
            {
                _process = process;
            }

            public static int ProcessCount
            {
                get { return _processes.Count; }
            }

            public Task Connect(IRequest request, string connectionId)
            {
                return OnConnected(request, connectionId);
            }

            public Task Disconnect(IRequest request, string connectionId)
            {
                return OnDisconnected(request, connectionId);
            }

            public Task Receive(IRequest request, string connectionId, string data)
            {
                return OnReceived(request, connectionId, data);
            }

            public void Dispose()
            {
                _processes.Clear();
            }

            protected override IProcess CreateProcess(string connectionId, string shell)
            {
                return _process;
            }
        }
    }
}