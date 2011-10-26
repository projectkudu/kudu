using System.Collections.Generic;
using System.IO.Abstractions;
using Kudu.Core.Commands;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class CommandExecutorTest
    {
        [Fact]
        public void GetMappedPathReplacesShortestPrefixThatExistsWithDrive()
        {
            var fs = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            directory.Setup(m => m.Exists(@"\\foo")).Returns(true);
            directory.Setup(m => m.Exists(@"\\foo\bar")).Returns(true);
            fs.Setup(m => m.Directory).Returns(directory.Object);
            var commandRunner = new MockCommandExecutor(fs.Object, @"\\foo\bar\baz");

            string path = commandRunner.GetMappedPath(@"\\foo\bar\baz");

            Assert.Equal(@"A:\bar\baz", path);
        }

        [Fact]
        public void GetMappedPathUsesMappedDriveIfPrefixIsTheSame()
        {
            var fs = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            directory.Setup(m => m.Exists(@"\\foo\bar")).Returns(true);
            fs.Setup(m => m.Directory).Returns(directory.Object);
            var commandRunner = new MockCommandExecutor(fs.Object, @"\\foo\bar\baz");

            string path1 = commandRunner.GetMappedPath(@"\\foo\bar\a");
            string path2 = commandRunner.GetMappedPath(@"\\foo\bar\z\emr");
            string path3 = commandRunner.GetMappedPath(@"\\foo\bar\b\d\f");

            Assert.Equal(@"A:\a", path1);
            Assert.Equal(@"A:\z\emr", path2);
            Assert.Equal(@"A:\b\d\f", path3);
        }

        private class MockCommandExecutor : CommandExecutor
        {
            private Dictionary<string, string> _mapping = new Dictionary<string, string>();
            private bool[] _drives = new bool[26];
            private int _used;

            public MockCommandExecutor(IFileSystem fileSystem, string workingDirectory)
                : base(fileSystem, workingDirectory)
            {
            }

            protected override bool MapPath(string path, out string driveName)
            {
                if (_used >= _drives.Length)
                {
                    driveName = null;
                    return false;
                }

                _drives[_used] = true;
                driveName = (char)(_used + 'A') + @":\";
                _used++;

                _mapping[path] = driveName;
                return true;
            }

            protected override bool TryGetMappedPath(string path, out string driveName)
            {
                return _mapping.TryGetValue(path, out driveName);
            }
        }
    }
}
