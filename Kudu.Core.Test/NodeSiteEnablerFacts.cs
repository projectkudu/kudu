using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using Kudu.Core.Deployment;
using Xunit;

namespace Kudu.Core.Test
{
    public class NodeSiteEnablerFacts
    {
        [Fact]
        public void TestJustServerJs()
        {
            TestJustOneStartFile("server.js");
        }

        [Fact]
        public void TestJustAppJs()
        {
            TestJustOneStartFile("app.js");
        }

        public void TestJustOneStartFile(string existingStartFile)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\site\" + existingStartFile, new MockFileData("some js") },
                { @"c:\site\foo.blah", new MockFileData("some file") },
            });

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, @"c:\repo", @"c:\site");

            Assert.True(nodeSiteEnabler.NeedNodeHandling());

            string startFile = nodeSiteEnabler.GetNodeStartFile();
            Assert.Equal(existingStartFile, startFile);

            Assert.False(fileSystem.File.Exists(@"c:\site\web.config"));
            nodeSiteEnabler.CreateConfigFile(startFile);
            Assert.True(fileSystem.File.Exists(@"c:\site\web.config"));

            Assert.True(fileSystem.File.ReadAllText(@"c:\site\web.config").Contains(existingStartFile));
        }

        [Fact]
        public void TestServerJsWithExistingRepoConfig()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\site\server.js", new MockFileData("some js") },
                { @"c:\repo\web.config", new MockFileData("some config") },
            });

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, @"c:\repo", @"c:\site");

            Assert.False(nodeSiteEnabler.NeedNodeHandling());
        }

        [Fact]
        public void TestServerJsWithExistingSiteConfig()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\site\server.js", new MockFileData("some js") },
                { @"c:\site\web.config", new MockFileData("some config") },
            });

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, @"c:\repo", @"c:\site");

            Assert.True(nodeSiteEnabler.NeedNodeHandling());

            nodeSiteEnabler.CreateConfigFile("server.js");

            Assert.True(fileSystem.File.ReadAllText(@"c:\site\web.config").Contains("server.js"));
        }


        [Fact]
        public void TestJsFileWithExtraFiles()
        {
            TestJsFileWithExtraNonNodeFile("foo.aspx");
            TestJsFileWithExtraNonNodeFile("foo.html");
            TestJsFileWithExtraNonNodeFile("foo.htm");
            TestJsFileWithExtraNonNodeFile("foo.cshtml");
            TestJsFileWithExtraNonNodeFile("foo.php");
        }

        public void TestJsFileWithExtraNonNodeFile(string nonNodeFile)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\site\hello.js", new MockFileData("some js") },
                { @"c:\site\" + nonNodeFile, new MockFileData("some file") },
            });

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, @"c:\repo", @"c:\site");

            Assert.False(nodeSiteEnabler.NeedNodeHandling());
        }

        [Fact]
        public void TestJsFileWithExtraRandomFile()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\site\hello.js", new MockFileData("some js") },
                { @"c:\site\foo.blah", new MockFileData("some file") },
            });

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, @"c:\repo", @"c:\site");

            Assert.True(nodeSiteEnabler.NeedNodeHandling());

            Assert.Equal(null, nodeSiteEnabler.GetNodeStartFile());
        }

        [Fact]
        public void TestJsFileWithAspxAndNodesModuleFolder()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\site\hello.js", new MockFileData("some js") },
                { @"c:\site\hello.aspx", new MockFileData("some aspx") },
                { @"c:\site\node_modules\foo.txt", new MockFileData("some file") },
            });

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, @"c:\repo", @"c:\site");

            Assert.True(nodeSiteEnabler.NeedNodeHandling());

            Assert.Equal(null, nodeSiteEnabler.GetNodeStartFile());
        }
    }
}
