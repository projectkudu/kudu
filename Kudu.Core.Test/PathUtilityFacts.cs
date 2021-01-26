using Kudu.Contracts.Infrastructure;
using Kudu.Core.Infrastructure;
using Moq;
using System.IO;
using System.IO.Abstractions;
using Xunit;

namespace Kudu.Core.Test
{
    public class PathUtilityFacts
    {
        [Fact]
        public void CleanFixesOtherSlashes()
        {
            Assert.Equal(@"c:\foo\bar", PathUtilityFactory.Instance.CleanPath(@"c:/foo/bar"));
        }

        [Fact]
        public void CleanTrimsTrailingSlashes()
        {
            Assert.Equal(@"c:\foo\BAR", PathUtilityFactory.Instance.CleanPath(@"c:/foo/BAR/"));
            Assert.Equal(@"c:\foo\bar", PathUtilityFactory.Instance.CleanPath(@"c:\foo\bar\"));
        }

        [Fact]
        public void CleanTrimsWhiteSpaces()
        {
            Assert.Equal(@"c:\foo\BAR", PathUtilityFactory.Instance.CleanPath(@" c:/foo/BAR/ "));
            Assert.Equal(@"c:\foo\bar", PathUtilityFactory.Instance.CleanPath("\tc:\\foo\\bar\\"));
        }

        [Theory]
        [InlineData("https://tempuri.org/hello/world", "https://tempuri.org/hello/world")]
        [InlineData("https://tempuri.org/hello?world", "https://tempuri.org/hello?...")]
        [InlineData("/tempuri.org/hello/world", "/tempuri.org/hello/world")]
        [InlineData("/tempuri.org/hello?world", "/tempuri.org/hello?...")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void ObfuscatePathTests(string uri, string expected)
        {
            var actual = StringUtils.ObfuscatePath(uri);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LatestMsBuildTest()
        {
            string[] msBuildDirs = { @"C:\Program Files (x86)\MSBuilds\MSBuild-15.8.9 - copy",
                                    @"C:\Program Files (x86)\MSBuilds\MSBuild-16.9.7",
                                    @"C:\Program Files (x86)\MSBuilds\MSBuild-16.9.9",
                                    @"C:\Program Files (x86)\MSBuilds\MSBuild-15.9.7"};

            string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            string expected = Path.Combine(programFiles, "MSBuilds", $"MSBuild-16.9.9", "MSBuild", "Current", "Bin");

            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            fileSystem.Setup(m => m.File).Returns(fileBase.Object);

            directory.Setup(m => m.GetDirectories(Path.Combine(programFiles, "MSBuilds"))).Returns(msBuildDirs);
            directory.Setup(m => m.Exists(expected)).Returns(true);

            fileBase.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds", $"MSBuild-15.8.9 - copy", $"MSBuild-15.8.9 - copy.installed"))).Returns(true);
            fileBase.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds", $"MSBuild-16.9.7", $"MSBuild-16.9.7.installed"))).Returns(true);
            fileBase.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds", $"MSBuild-16.9.9", $"MSBuild-16.9.9.installed"))).Returns(true);
            fileBase.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds", $"MSBuild-15.9.7", $"MSBuild-15.9.7.installed"))).Returns(true);

            FileSystemHelpers.Instance = fileSystem.Object;

            string result = PathUtilityFactory.Instance.ResolveMSBuild1670Dir();
            Assert.Equal(expected, result);
        }
    }
}
