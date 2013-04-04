using System;
using System.Collections.Generic;
using System.Threading;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class IdleManagerFacts
    {
        [Fact]
        public void WaitForExitWaitsForEOFPriorToExiting()
        {
            // Arrange
            var idleTimeout = DeploymentSettingsExtension.DefaultCommandIdleTimeout;
            var tracer = new Mock<ITracer>(MockBehavior.Strict);
            var idleManager = new IdleManager(idleTimeout, tracer.Object);
            var process = new Mock<IProcess>(MockBehavior.Strict);

            // Setup
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(true)
                   .Verifiable();
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
        }

        [Fact]
        public void WaitForExitPollsAllowsExecutableToContinueAfterTimeoutIfIOActivity()
        {
            // Arrange
            var idleTimeout = TimeSpan.MinValue;
            var tracer = new Mock<ITracer>(MockBehavior.Strict);
            var idleManager = new IdleManager(idleTimeout, tracer.Object);
            var process = new Mock<IProcess>(MockBehavior.Strict);

            // Setup
            int num = 10;
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(() =>
                    {
                        if (--num == 0)
                        {
                            return true;
                        }
                        else
                        {
                            Thread.Sleep(10);
                            idleManager.UpdateActivity();
                            return false;
                        }
                    });
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
            Assert.Equal(0, num);
        }

        [Fact]
        public void WaitForExitPollsAllowsExecutableToContinueAfterTimeoutIfCpuActivity()
        {
            // Arrange
            var idleTimeout = TimeSpan.MinValue;
            var tracer = new Mock<ITracer>(MockBehavior.Strict);
            var idleManager = new IdleManager(idleTimeout, tracer.Object);
            var process = new Mock<IProcess>(MockBehavior.Strict);

            // Setup
            int num = 10, cpu = 0;
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(() => --num == 0);
            process.Setup(f => f.GetTotalProcessorTime(It.IsAny<ITracer>()))
                   .Returns(() => TimeSpan.FromSeconds(++cpu))
                   .Verifiable();
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();
            tracer.Setup(t => t.Trace(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                  .Verifiable();

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
            Assert.Equal(0, num);
        }

        [Fact]
        public void WaitForExitPollsAllowsExecutableToContinueAfterTimeoutIfCpuOrIOActivity()
        {
            // Arrange
            var idleTimeout = TimeSpan.MinValue;
            var tracer = new Mock<ITracer>(MockBehavior.Strict);
            var idleManager = new IdleManager(idleTimeout, tracer.Object);
            var process = new Mock<IProcess>(MockBehavior.Strict);

            // Setup
            int num = 10;
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(() => --num == 0);
            process.Setup(f => f.GetTotalProcessorTime(It.IsAny<ITracer>()))
                   .Returns(() => 
                   {
                       Thread.Sleep(10);
                       idleManager.UpdateActivity(); 
                       return TimeSpan.FromSeconds(5); 
                   })
                   .Verifiable();
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();
            tracer.Setup(t => t.Trace(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                  .Verifiable();

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
            Assert.Equal(0, num);
        }

        [Fact]
        public void WaitForExitPollsKillsProcessIfProcessorTimeDoesNotChangeAndNotUpdated()
        {
            // Arrange
            var tracer = new Mock<ITracer>(MockBehavior.Strict);
            DateTime startTime = DateTime.UtcNow;
            TimeSpan idleTimeout = TimeSpan.FromMilliseconds(100);
            var idleManager = new IdleManager(idleTimeout, tracer.Object);
            var process = new Mock<IProcess>(MockBehavior.Strict);

            // Setup
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.SetupGet(f => f.Arguments)
                   .Returns("");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(() => { Thread.Sleep(10); return false; });
            process.Setup(f => f.GetTotalProcessorTime(It.IsAny<ITracer>()))
                   .Returns(TimeSpan.Zero);
            process.Setup(f => f.Kill(tracer.Object))
                   .Verifiable();
            tracer.Setup(t => t.Trace(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                  .Verifiable();

            // Act
            var ex = Assert.Throws<CommandLineException>(() => idleManager.WaitForExit(process.Object));

            // Assert
            process.Verify();

            Assert.True(DateTime.UtcNow - startTime >= idleTimeout);
            Assert.Contains("Command 'Test-Process ' aborted due to no output and CPU activity for", ex.Message);
        }
    }
}
