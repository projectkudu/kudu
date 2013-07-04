using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.Core.Test
{
    public class AsyncLockFileTests
    {
        private string _lockFilePath;
        private LockFile _lockFile;

        public AsyncLockFileTests()
        {
            _lockFilePath = Path.Combine(PathHelper.TestLockPath, "file.lock");
            _lockFile = new LockFile(_lockFilePath, NullTracerFactory.Instance, new FileSystem());
            _lockFile.InitializeAsyncLocks();
        }

        [Fact]
        public void AsyncLock_ThrowsIfNotInitialized()
        {
            string lockFilePath = Path.Combine(PathHelper.TestLockPath, "uninitialized.lock");
            LockFile uninitialized = new LockFile(lockFilePath, NullTracerFactory.Instance, new FileSystem());
            Assert.Throws<InvalidOperationException>(() => uninitialized.LockAsync());
        }

        [Fact]
        public void AsyncLock_BasicTest()
        {
            for (int i = 0; i < 2; i++)
            {
                // Acquire
                Assert.Equal(false, _lockFile.IsHeld);
                Assert.Equal(true, _lockFile.Lock());

                // Test
                Assert.Equal(true, _lockFile.IsHeld);
                Assert.Equal(false, _lockFile.Lock());

                // Release
                _lockFile.Release();
                Assert.Equal(false, _lockFile.IsHeld);
            }
        }

        [Fact]
        public void AsyncLock_ConcurrentLockFollowedByRelease()
        {
            ManualResetEvent done = new ManualResetEvent(false);
            const int MaxCount = 100;
            int count = 0;

            Parallel.For(0, MaxCount, async cnt =>
            {
                await _lockFile.LockAsync();

                // We are now within the lock
                await Task.Delay(5);
                count = count + 1;

                // Releasing lock
                _lockFile.Release();
                if (count == MaxCount)
                {
                    done.Set();
                }
            });

            done.WaitOne();
            Assert.Equal(MaxCount, count);
        }

        [Fact]
        public async Task LockAsync_SequentialLockFollowedByRelease()
        {
            List<Task> lockRequests = new List<Task>();
            for (int cnt = 0; cnt < 100; cnt++)
            {
                Task lockRequest = _lockFile.LockAsync();
                lockRequests.Add(lockRequest);
            }

            for (int cnt = 0; cnt < 100; cnt++)
            {
                await lockRequests[cnt];
                _lockFile.Release();
            }

            Assert.Equal(false, _lockFile.IsHeld);
        }

        [Fact]
        public void LockAsync_ThrowsAfterTerminateAsyncLocks()
        {
            _lockFile.TerminateAsyncLocks();
            Assert.Throws<InvalidOperationException>(() => _lockFile.LockAsync());
        }
    }
}