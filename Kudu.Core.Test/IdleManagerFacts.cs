using System;
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
            var process = new Mock<IProcess>(MockBehavior.Strict);
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(true)
                   .Verifiable();
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();
            var idleManager = new IdleManager(TimeSpan.FromSeconds(5), Mock.Of<ITracer>());

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
        }

        [Fact]
        public void WaitForExitPollsAllowsExecutableToContinueAfterTimeoutIfItIsBusy()
        {
            // Arrange
            TimeSpan idleTimeout = TimeSpan.FromMinutes(10);
            IdleManager idleManager = new IdleManager(idleTimeout, Mock.Of<ITracer>());
            var process = new Mock<IProcess>(MockBehavior.Strict);
            process.Setup(f => f.WaitForExit(idleTimeout))
                   .Returns(false)
                   .Verifiable();
            int num = 0;
            process.Setup(f => f.WaitForExit(TimeSpan.FromSeconds(10)))
                   .Returns(() =>
                    {
                        if (num++ == 3)
                        {
                            return true;
                        }
                        else
                        {
                            idleManager.UpdateActivity();
                            return false;
                        }
                    });
            process.Setup(f => f.GetTotalProcessorTime())
                   .Returns(num);
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
        }

        [Fact]
        public void WaitForExitPollsAllowsExecutableToContinueAsLongAsItIsPerformingSomeCPUOrUpdating()
        {
            // Arrange
            TimeSpan idleTimeout = TimeSpan.FromMinutes(10);
            IdleManager idleManager = new IdleManager(idleTimeout, Mock.Of<ITracer>());
            var process = new Mock<IProcess>(MockBehavior.Strict);
            process.Setup(f => f.WaitForExit(idleTimeout))
                   .Returns(false);
            int num = 0, cpu = 0;
            process.Setup(f => f.WaitForExit(TimeSpan.FromSeconds(10)))
                   .Returns(() =>
                   {
                       if (num++ == 10)
                       {
                           return true;
                       }
                       else if (num % 2 == 0)
                       {
                           idleManager.UpdateActivity();
                       }
                       else
                       {
                           cpu++;
                       }
                       return false;
                   });
            process.Setup(f => f.GetTotalProcessorTime())
                   .Returns(cpu);
            process.Setup(f => f.WaitUntilEOF())
                   .Verifiable();

            // Act
            idleManager.WaitForExit(process.Object);

            // Assert
            process.Verify();
        }

        [Fact]
        public void WaitForExitPollsKillsProcessIfProcessorTimeDoesNotChangeAndNotUpdated()
        {
            // Arrange
            var tracer = Mock.Of<ITracer>();
            var idleTimeout = TimeSpan.FromMinutes(10);
            IdleManager idleManager = new IdleManager(idleTimeout, tracer, DateTime.UtcNow.AddMinutes(-1));
            var process = new Mock<IProcess>(MockBehavior.Strict);
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.SetupGet(f => f.Arguments)
                   .Returns("");
            process.Setup(f => f.WaitForExit(idleTimeout))
                   .Returns(false);
            process.Setup(f => f.WaitForExit(TimeSpan.FromSeconds(10)))
                   .Returns(false);
            process.Setup(f => f.GetTotalProcessorTime())
                   .Returns(5);
            process.Setup(f => f.Kill(tracer))
                   .Verifiable();

            // Act
            var ex = Assert.Throws<CommandLineException>(() => idleManager.WaitForExit(process.Object));

            // Assert
            process.Verify();

            Assert.Contains("Command 'Test-Process ' aborted due to idle timeout after", ex.Message);
        }

        [Fact]
        public void WaitForExitPollsKillsProcessIfItUpdatesActivityForOver30Minutes()
        {
            // Arrange
            var tracer = Mock.Of<ITracer>();
            IdleManager idleManager = new IdleManager(TimeSpan.FromMinutes(10), tracer);
            var process = new Mock<IProcess>(MockBehavior.Strict);
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.SetupGet(f => f.Arguments)
                   .Returns("");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Callback(() => idleManager.UpdateActivity())
                   .Returns(false);
            process.Setup(f => f.GetTotalProcessorTime())
                   .Returns(5);
            process.Setup(f => f.Kill(tracer))
                   .Verifiable();

            // Act
            var ex = Assert.Throws<CommandLineException>(() => idleManager.WaitForExit(process.Object));

            // Assert
            process.Verify();

            Assert.Equal("Command 'Test-Process ' aborted due to idle timeout after '1800' seconds.\r\nTest-Process", ex.Message.TrimEnd());
        }

        [Fact]
        public void WaitForExitPollsKillsProcessIfItIsCosntantlyUsingCPUForOver30Minutes()
        {
            // Arrange
            var tracer = Mock.Of<ITracer>();
            IdleManager idleManager = new IdleManager(TimeSpan.FromMinutes(10), tracer);
            var process = new Mock<IProcess>(MockBehavior.Strict);
            long i = 0;
            process.SetupGet(f => f.Name)
                   .Returns("Test-Process");
            process.SetupGet(f => f.Arguments)
                   .Returns("");
            process.Setup(f => f.WaitForExit(It.IsAny<TimeSpan>()))
                   .Returns(false);
            process.Setup(f => f.GetTotalProcessorTime())
                   .Returns(() => ++i);
            process.Setup(f => f.Kill(tracer))
                   .Verifiable();

            // Act
            var ex = Assert.Throws<CommandLineException>(() => idleManager.WaitForExit(process.Object));

            // Assert
            process.Verify();

            Assert.Equal("Command 'Test-Process ' aborted due to idle timeout after '1800' seconds.\r\nTest-Process", ex.Message.TrimEnd());
        }
    }
}
