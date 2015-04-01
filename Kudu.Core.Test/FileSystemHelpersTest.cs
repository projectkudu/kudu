using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;

namespace Kudu.Core.Test
{
    public class FileSystemHelpersTest
    {
        [Fact]
        public void EnsureDirectoryCreatesDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(false);
            FileSystemHelpers.Instance = fileSystem.Object;

            string path = FileSystemHelpers.EnsureDirectory("foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Once());
        }

        [Fact]
        public void EnsureDirectoryDoesNotCreateDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(true);
            FileSystemHelpers.Instance = fileSystem.Object;

            string path = FileSystemHelpers.EnsureDirectory("foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Never());
        }

        [Theory]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo\bar", true)]
        [InlineData(@"x:\temp\bar", @"x:\temp\Foo", false)]
        [InlineData(@"x:\temp\Foo\bar", @"x:\temp\foo", false)]
        [InlineData(@"x:\temp\foo\", @"x:\temp\Foo\", true)]
        [InlineData(@"x:\temp\Foo\", @"x:\temp\foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo\", true)]
        [InlineData(@"x:\temp\Foo", @"x:\temp\foobar", false)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foobar\", false)]
        [InlineData(@"x:\temp\foo\..", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp\..\temp\foo\..", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo\..", false)]
        // slashes
        [InlineData(@"x:/temp\foo", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp/foo", @"x:\temp\Foo\bar", true)]
        [InlineData(@"x:\temp\bar", @"x:/temp\Foo", false)]
        [InlineData(@"x:\temp\Foo\bar", @"x:\temp/foo", false)]
        [InlineData(@"x:/temp\foo\", @"x:\temp\Foo\", true)]
        [InlineData(@"x:\temp/Foo\", @"x:\temp\foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp/Foo\", true)]
        [InlineData(@"x:\temp/Foo", @"x:\temp\foobar", false)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foobar/", false)]
        [InlineData(@"x:\temp\foo/..", @"x:/temp\Foo", true)]
        [InlineData(@"x:\temp\..\temp/foo\..", @"x:/temp\Foo", true)]
        [InlineData(@"x:\temp/foo", @"x:\temp\Foo\..", false)]
        public void IsSubfolderOfTests(string parent, string child, bool expected)
        {
            Assert.Equal(expected, FileSystemHelpers.IsSubfolder(parent, child));
        }
    }
}
