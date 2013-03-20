using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class OperationLockTests
    {
        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 0)]
        public void LockBasicTest(bool isHeld, int expected)
        {
            // Mock
            var lockObj = MockOperationLock(isHeld);
            var actual = 0;

            // Test
            var success = lockObj.TryLockOperation(() => ++actual, TimeSpan.Zero);

            // Assert
            Assert.NotEqual(isHeld, success);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LockMultipleTest()
        {
            // Mock
            var lockObj = MockOperationLock();
            var actual = 0;
            var threads = 5;
            var tasks = new List<Task>();

            // Test
            for (int i = 0; i < threads; i++)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    bool succeed = lockObj.TryLockOperation(() =>
                    {
                        // to simulate delay get and set
                        var temp = actual;
                        Thread.Sleep(500);
                        actual = temp + 1;
                    }, TimeSpan.FromSeconds(10));

                    // Assert
                    Assert.True(succeed);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(threads, actual);
        }

        [Fact]
        public void LockTimeoutTest()
        {
            // Mock
            var lockObj = MockOperationLock();
            var actual = 0;
            var threads = 2;
            var tasks = new List<Task<bool>>();

            // Test
            for (int i = 0; i < threads; i++)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    return lockObj.TryLockOperation(() =>
                    {
                        // to simulate delay get and set
                        var temp = actual;
                        Thread.Sleep(5000);
                        actual = temp + 1;
                    }, TimeSpan.FromSeconds(1));
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.True(tasks[0].Result != tasks[1].Result);
            Assert.Equal(1, actual);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 0)]
        public void LockBasicWithResultTest(bool isHeld, int expected)
        {
            // Mock
            var lockObj = MockOperationLock(isHeld);
            var actual = 0;

            if (isHeld)
            {
                // Test
                Assert.Throws<InvalidOperationException>(() => lockObj.LockOperation(() => actual + 1, TimeSpan.Zero));
            }
            else
            {
                // Test
                actual = lockObj.LockOperation(() => actual + 1, TimeSpan.Zero);
            }

            // Assert
            Assert.Equal(expected, actual);
        }

        public IOperationLock MockOperationLock(bool isHeld = false)
        {
            var locked = isHeld ? 1 : 0;
            var lockObj = new Mock<IOperationLock>();

            lockObj.Setup(l => l.IsHeld)
                   .Returns(() => locked != 0);
            lockObj.Setup(l => l.Lock())
                   .Returns(() => 0 == Interlocked.CompareExchange(ref locked, 1, 0));
            lockObj.Setup(l => l.Release())
                   .Returns(() =>
                    {
                        Assert.Equal(1, locked);
                        locked = 0;
                        return true;
                    });

            return lockObj.Object;
        }
    }
}
