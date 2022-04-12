using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Moq;
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

        [Theory]
        [InlineData("https://tempuri.org/hello/world", "https://tempuri.org/hello/world")]
        [InlineData("https://tempuri.org/hello?world", "https://tempuri.org/hello?...")]
        [InlineData("https://user:password@tempuri.org:545/hello?world", "https://tempuri.org:545/hello?...")]
        [InlineData("/tempuri.org/hello/world", "/tempuri.org/hello/world")]
        [InlineData("/tempuri.org/hello?world", "/tempuri.org/hello?...")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void ObfuscateUrlTests(string uri, string expected)
        {
            var actual = StringUtils.ObfuscateUrl(uri);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LatestMsBuildTest()
        {
            var msBuildDirs = new[]
            { 
                "x.y.z",
                "16.9.7",
                "16.9.10",
                "16.9.9",
                "15.9.7"
            };

            string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            string expected = Path.Combine(programFiles, "MSBuilds", "16.9.10", "MSBuild", "Current", "Bin");

            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            fileSystem.Setup(m => m.File).Returns(fileBase.Object);

            directory.Setup(m => m.GetDirectories(Path.Combine(programFiles, "MSBuilds")))
                .Returns(msBuildDirs.Select(d => Path.Combine(programFiles, "MSBuilds", d)).ToArray());
            directory.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds"))).Returns(true);

            foreach (var dir in msBuildDirs)
            {
                directory.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds", dir, "MSBuild", "Current", "Bin"))).Returns(true);
                fileBase.Setup(m => m.Exists(Path.Combine(programFiles, "MSBuilds", $"{dir}.installed"))).Returns(true);
            }

            FileSystemHelpers.Instance = fileSystem.Object;

            string result = PathUtilityFactory.Instance.ResolveMSBuild1670Dir();
            Assert.Equal(expected, result);

            expected = Path.Combine(programFiles, "MSBuilds", "16.9.9", "MSBuild", "Current", "Bin");
            ScmHostingConfigurations.Config = new Dictionary<string, string>
            {
                { "SCM_MSBUILD_VERSION",  "16.9.9" }
            };
            try
            {
                result = PathUtilityFactory.Instance.ResolveMSBuild1670Dir();
                Assert.Equal(expected, result);
            }
            finally
            {
                ScmHostingConfigurations.Config = null;
            }
        }

        [Theory]
        [InlineData("netcoreapp3.1", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("netcoreapp3.2", false)]
        [InlineData("netcoreapp3.11", true)]
        public void VsHelperIsDotNet31VersionTests(string target, bool expected)
        {
            var actual = VsHelper.IsDotNet31Version(target);
            Assert.Equal(expected, actual);
        }
    }
}
