using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class BaseJobRunnerFacts
    {
        private readonly string _testJobSourceDir;
        private readonly string _testJobWorkingDir;

        public BaseJobRunnerFacts()
        {
            _testJobSourceDir = Path.Combine(Path.GetTempPath(), "testjobsource");
            _testJobWorkingDir = Path.Combine(Path.GetTempPath(), "testjobworking");
        }

        [Fact]
        public void JobDirectoryHasChanged_NoChages_NoCachedEntries_ReturnsFalse()
        {
            CreateTestJobDirectories();

            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            Assert.Equal(6, sourceDirectoryFileMap.Count);

            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            Assert.Equal(6, workingDirectoryFileMap.Count);

            Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, null));
        }

        [Fact]
        public void JobDirectoryHasChanged_NoChanges_CachedEntries_ReturnsFalse()
        {
            CreateTestJobDirectories();

            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            var cachedDirectoryFileMap = workingDirectoryFileMap;

            Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInSubDir_ReturnsTrue()
        {
            CreateTestJobDirectories();

            string testSubDir = Path.Combine(_testJobSourceDir, "subdir");
            File.WriteAllText(Path.Combine(testSubDir, "test2.txt"), "update");

            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            var cachedDirectoryFileMap = workingDirectoryFileMap;

            Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInRootDir_ReturnsTrue()
        {
            CreateTestJobDirectories();

            File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "update");

            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            var cachedDirectoryFileMap = workingDirectoryFileMap;

            Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));
        }

        [Fact]
        public void JobDirectoryHasChanged_FileAddedInRootDir_ReturnsTrue()
        {
            CreateTestJobDirectories();

            File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "test");

            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            var cachedDirectoryFileMap = workingDirectoryFileMap;

            Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));
        }

        [Fact]
        public void JobDirectoryHasChanged_FileDeleted_ReturnsTrue()
        {
            CreateTestJobDirectories();

            File.Delete(Path.Combine(_testJobSourceDir, "test2.txt"));

            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            var cachedDirectoryFileMap = workingDirectoryFileMap;

            Assert.True(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));
        }

        [Fact]
        public void JobDirectoryHasChanged_FileChangesInWorkingDir_ReturnsFalse()
        {
            CreateTestJobDirectories();

            // add a file
            File.WriteAllText(Path.Combine(_testJobWorkingDir, "test4.txt"), "test");
            var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            var cachedDirectoryFileMap = sourceDirectoryFileMap;
            Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));

            // modify a file
            File.WriteAllText(Path.Combine(_testJobWorkingDir, "test2.txt"), "test");
            sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobSourceDir);
            workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileMap(_testJobWorkingDir);
            cachedDirectoryFileMap = sourceDirectoryFileMap;
            Assert.False(BaseJobRunner.JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap));
        }

        private void CreateTestJobDirectories()
        {
            if (Directory.Exists(_testJobSourceDir))
            {
                Directory.Delete(_testJobSourceDir, true);
            }
            Directory.CreateDirectory(_testJobSourceDir);

            // add some files in root
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test1.txt"), "test");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "test");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test3.txt"), "test");

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
        }
    }
}
