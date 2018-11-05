using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class LockFileTests
    {
        [Fact]
        public void LockFileBasicTest()
        {
            // Mock
            var lockFile = MockLockFile(@"x:\path\file.lock");
            var lockFile2 = MockLockFile(@"x:\path\file.lock");

            for (int i = 0; i < 2; i++)
            {
                // Acquire
                Assert.Equal(false, lockFile.IsHeld);
                Assert.Equal(true, lockFile.Lock("operationName"));

                // Test
                Assert.Equal(true, lockFile.IsHeld);
                Assert.Equal(false, lockFile.Lock("operationName"));

                Assert.Equal(true, lockFile2.IsHeld);
                Assert.Equal(false, lockFile2.Lock("operationName"));

                // Release
                lockFile.Release();
                Assert.Equal(false, lockFile.IsHeld);
                Assert.Equal(false, lockFile2.IsHeld);
            }
        }

        [Fact]
        public void LockFileConcurrentTest()
        {
            // Mock
            var file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var lockFile = new LockFile(file, NullTracerFactory.Instance);
            int count = 10;
            int threads = 2;
            int totals = 0;

            // Test
            var result = Parallel.For(0, threads, i =>
            {
                for (int j = 0; j < count; ++j)
                {
                    lockFile.LockOperation(() =>
                    {
                        // simulate get/set
                        int val = totals;
                        Thread.Sleep(0);
                        totals = val + 1;

                        using (var reader = new StreamReader(FileSystemHelpers.OpenFile(file, FileMode.Open, FileAccess.Read, FileShare.Write)))
                        {
                            Assert.Contains(" Kudu.Core.Test.LockFileTests", reader.ReadToEnd());
                        }

                    }, "operationName", TimeSpan.FromSeconds(60));
                }
            });

            // Assert
            Assert.True(result.IsCompleted);
            Assert.Equal(count * threads, totals);
            FileSystemHelpers.DeleteFileSafe(file);
        }

        [Fact]
        public void LockFileRepositoryClearLockTest()
        {
            // Mock
            var path = @"x:\temp";
            var lockFileName = Path.Combine(path, "deployment.lock");
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();
            var directory = new Mock<DirectoryBase>();
            var repository = new Mock<IRepository>();
            var repositoryFactory = new Mock<IRepositoryFactory>();
            var lockFile = new DeploymentLockFile(lockFileName, Mock.Of<ITraceFactory>());

            // Setup
            fileSystem.SetupGet(f => f.Directory)
                      .Returns(directory.Object);
            directory.Setup(d => d.Exists(path))
                     .Returns(true);
            fileSystem.SetupGet(f => f.File)
                      .Returns(file.Object);
            file.Setup(f => f.Open(lockFileName, FileMode.Create, FileAccess.Write, FileShare.Read))
                .Returns(Mock.Of<Stream>());
            repositoryFactory.Setup(f => f.GetRepository())
                             .Returns(repository.Object);
            FileSystemHelpers.Instance = fileSystem.Object;

            // Test
            lockFile.RepositoryFactory = repositoryFactory.Object;
            var locked = lockFile.Lock("operationName");

            // Assert
            Assert.True(locked, "lock should be successful!");
            repository.Verify(r => r.ClearLock(), Times.Once);
        }

        private LockFile MockLockFile(string path, ITraceFactory traceFactory = null, IFileSystem fileSystem = null)
        {
            traceFactory = traceFactory ?? NullTracerFactory.Instance;

            if (fileSystem == null)
            {
                var locked = false;
                var stream = new Mock<Stream>(MockBehavior.Strict);
                var fs = new Mock<IFileSystem>(MockBehavior.Strict);
                stream.Setup(s => s.Close())
                      .Callback(() =>
                      {
                          locked = false;
                      });
                stream.Setup(s => s.Write(It.IsAny<byte[]>(), 0, It.IsAny<int>()));
                stream.Setup(s => s.Flush());

                fs.Setup(f => f.Directory.Exists(Path.GetDirectoryName(path)))
                  .Returns(true);
                fs.Setup(f => f.File.Exists(path))
                  .Returns(() => true);
                fs.Setup(f => f.File.Delete(path));
                fs.Setup(f => f.File.Open(path, It.IsAny<FileMode>(), FileAccess.Write, FileShare.Read))
                  .Returns(() =>
                  {
                      lock (stream)
                      {
                          if (locked)
                          {
                              throw new IOException();
                          }
                          locked = true;
                          return stream.Object;
                      }
                  });

                fileSystem = fs.Object;
            }

            FileSystemHelpers.Instance = fileSystem;

            return new LockFile(path, traceFactory);
        }

        [Fact]
        public void LockFailOnLockAcquiredTest()
        {
            // Mock
            var file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var lockFile = new FailOnLockAcquiredLock(file);

            // 1st attempt lock unsuccessful since OnLockAcquired failed
            Assert.False(lockFile.Lock("operationName"));

            // Next attempt successful
            Assert.True(lockFile.Lock("operationName"));
            lockFile.Release();

            FileSystemHelpers.DeleteFileSafe(file);
        }

        class FailOnLockAcquiredLock : LockFile
        {
            private int _attempt = 0;

            public FailOnLockAcquiredLock(string path)
                : base(path)
            {
            }

            protected override void OnLockAcquired()
            {
                if (_attempt++ == 0)
                {
                    throw new InvalidOperationException("fail OnLockAcquired!");
                }
            }
        }
    }
}