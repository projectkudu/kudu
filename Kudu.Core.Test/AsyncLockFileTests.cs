using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class AsyncLockFileTests
    {
        private string _lockFilePath;
        private DeploymentLockFile _lockFile;

        public AsyncLockFileTests()
        {
            _lockFilePath = Path.Combine(PathHelper.TestLockPath, "file.lock");
            _lockFile = new DeploymentLockFile(_lockFilePath, NullTracerFactory.Instance);
            _lockFile.InitializeAsyncLocks();
        }

        [Fact]
        public async Task AsyncLock_ThrowsIfNotInitialized()
        {
            string lockFilePath = Path.Combine(PathHelper.TestLockPath, "uninitialized.lock");
            LockFile uninitialized = new LockFile(lockFilePath, NullTracerFactory.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => uninitialized.LockAsync());
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

            Assert.True(done.WaitOne(10000), "lock timeout!");
            Assert.Equal(MaxCount, count);
        }

        [Fact]
        public async Task LockAsync_SequentialLockFollowedByRelease()
        {
            // Mock
            var repository = new Mock<IRepository>();
            var repositoryFactory = new Mock<IRepositoryFactory>();
            repositoryFactory.Setup(f => f.GetRepository())
                             .Returns(repository.Object);
            _lockFile.RepositoryFactory = repositoryFactory.Object;

            try
            {
                var maxCount = 100;
                List<Task> lockRequests = new List<Task>();
                for (int cnt = 0; cnt < maxCount; cnt++)
                {
                    Task lockRequest = _lockFile.LockAsync();
                    lockRequests.Add(lockRequest);
                }

                for (int cnt = 0; cnt < maxCount; cnt++)
                {
                    await lockRequests[cnt];
                    _lockFile.Release();
                }

                Assert.Equal(false, _lockFile.IsHeld);
                repository.Verify(r => r.ClearLock(), Times.Exactly(maxCount));
            }
            finally
            {
                _lockFile.RepositoryFactory = null;
            }
        }

        [Fact]
        public async Task LockAsync_ThrowsAfterTerminateAsyncLocks()
        {
            _lockFile.TerminateAsyncLocks();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _lockFile.LockAsync());
        }
    }
}