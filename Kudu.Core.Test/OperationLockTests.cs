using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Xunit;

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
            var lockObj = new MockOperationLock(isHeld);
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
            var lockObj = new MockOperationLock();
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
            var lockObj = new MockOperationLock();
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
            var lockObj = new MockOperationLock(isHeld);
            var actual = 0;

            if (isHeld)
            {
                // Test
                Assert.Throws<LockOperationException>(() => lockObj.LockOperation(() => actual + 1, TimeSpan.Zero));
            }
            else
            {
                // Test
                actual = lockObj.LockOperation(() => actual + 1, TimeSpan.Zero);
            }

            // Assert
            Assert.Equal(expected, actual);
        }

        public class MockOperationLock : IOperationLock
        {
            private int _locked;

            public MockOperationLock(bool isHeld = false)
            {
                _locked = isHeld ? 1 : 0;
            }

            public bool IsHeld
            {
                get { return _locked != 0; }
            }

            public bool Lock()
            {
                return Interlocked.CompareExchange(ref _locked, 1, 0) == 0;
            }

            public async Task LockAsync()
            {
                while (true)
                {
                    if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
                    {
                        return;
                    }
                    await Task.Delay(100);
                }
            }

            public void Release()
            {
                Assert.Equal(1, _locked);
                _locked = 0;
            }
        }
    }
}
