using System;
using System.Configuration;
using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class BaseJobRunnerFacts
    {
        private readonly string _testJobSourceDir;
        private readonly string _testJobWorkingDir;
        private readonly Mock<IJobLogger> _mockLogger;

        public BaseJobRunnerFacts()
        {
            _testJobSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "testjobsource");
            _testJobWorkingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "testjobworking");
            _mockLogger = new Mock<IJobLogger>(MockBehavior.Strict);
        }

        [Fact]
        public void JobDirectoryHasChanged_NoChanges_CachedEntries_ReturnsFalse()
        {
            using (CreateTestJobDirectories())
            {
                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                Assert.Equal(8, sourceDirectoryFileMap.Count);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                Assert.Equal(8, workingDirectoryFileMap.Count);
                var cachedDirectoryFileMap = workingDirectoryFileMap;

                Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInSubDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                string testSubDir = Path.Combine(_testJobSourceDir, "subdir");
                File.WriteAllText(Path.Combine(testSubDir, "test2.txt"), "update");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test2.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInRootDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "update");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileAddedInRootDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test4.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileDeleted_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobSourceDir, "test2.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' has been deleted."));

                Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileAddedInWorkingDir_ReturnsFalse()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobWorkingDir, "test4.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;

                Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInWorkingDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobWorkingDir, "test2.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileDeletedInWorkingDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobWorkingDir, "test2.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_IsCaseInsensitive()
        {
            using (CreateTestJobDirectories())
            {
                // create a case mismatch
                File.Move(Path.Combine(_testJobWorkingDir, "test1.txt"), Path.Combine(_testJobWorkingDir, "TEST1.TXT"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;

                Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, _mockLogger.Object));

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void UpdateAppConfigs_DoesNotModifyLastWriteTime()
        {
            using (CreateTestJobDirectories())
            {
                FileInfo fileInfo = new FileInfo(Path.Combine(_testJobWorkingDir, "job.exe.config"));
                DateTime before = fileInfo.LastWriteTimeUtc;

                SettingsProcessor.Instance.AppSettings.Add("test", "test");

                Mock<IAnalytics> mockAnalytics = new Mock<IAnalytics>();
                BaseJobRunner.UpdateAppConfigs(_testJobWorkingDir, mockAnalytics.Object);

                fileInfo.Refresh();
                DateTime after = fileInfo.LastWriteTimeUtc;
                Assert.Equal(before, after);

                Configuration config = ConfigurationManager.OpenExeConfiguration(Path.Combine(_testJobWorkingDir, "job.exe"));
                Assert.Equal("test", config.AppSettings.Settings["test"].Value);
            }
        }

        private DisposableAction CreateTestJobDirectories()
        {
            var cleanupAction = new Action(() =>
            {
                if (Directory.Exists(_testJobSourceDir))
                {
                    Directory.Delete(_testJobSourceDir, true);
                }
                if (Directory.Exists(_testJobWorkingDir))
                {
                    Directory.Delete(_testJobWorkingDir, true);
                }
            });

            Directory.CreateDirectory(_testJobSourceDir);

            // add some files in root
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test1.txt"), "test");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "test");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test3.txt"), "test");

            File.WriteAllText(Path.Combine(_testJobSourceDir, "job.exe"), "binary");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "job.exe.config"), "<configuration></configuration>");

            // add some files in a sub directory
            string testSubDir = Path.Combine(_testJobSourceDir, "subdir");
            Directory.CreateDirectory(testSubDir);
            File.WriteAllText(Path.Combine(testSubDir, "test1.txt"), "test");
            File.WriteAllText(Path.Combine(testSubDir, "test2.txt"), "test");
            File.WriteAllText(Path.Combine(testSubDir, "test3.txt"), "test");

            // now, copy all the files to the working directory
            if (Directory.Exists(_testJobWorkingDir))
            {
                Directory.Delete(_testJobWorkingDir, true);
            }
            FileSystemHelpers.CopyDirectoryRecursive(_testJobSourceDir, _testJobWorkingDir);

            return new DisposableAction(cleanupAction);
        }
    }
}
