using System;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;

//             "default.htm", "default.html", "default.asp", "index.htm", "index.html", "iisstart.htm", "default.aspx", "index.php"

namespace Kudu.Core.Deployment.Generator.Test
{
    public class NodeSiteEnablerTests
    {
        [Theory]
        [InlineData(true, new string[] { "server.js" })]
        [InlineData(true, new string[] { "app.js" })]
        [InlineData(true, new string[] { "package.json" })]
        [InlineData(false, new string[] { "server.js", "default.html" })]
        [InlineData(true, new string[] { "server.js", "site.html" })]
        [InlineData(false, new string[] { "server.js", "default.html" })]
        [InlineData(false, new string[] { "app.js", "default.htm" })]
        [InlineData(true, new string[] { "package.json", "default.asp" })]
        [InlineData(false, new string[] { "server.js", "index.htm" })]
        [InlineData(false, new string[] { "server.js", "index.html" })]
        [InlineData(false, new string[] { "server.js", "iisstart.htm" })]
        [InlineData(false, new string[] { "app.js", "default.aspx" })]
        [InlineData(false, new string[] { "app.js", "index.php" })]
        [InlineData(true, new string[] { "package.json", "site.aspx" })]
        [InlineData(false, new string[0])]
        [InlineData(false, new string[] { "index.php" })]
        [InlineData(false, new string[] { "site.aspx" })]
        [InlineData(false, new string[] { "site.aspx", "index2.aspx" })]
        public void TestLooksLikeNode(bool looksLikeNodeExpectedResult, string[] existingFiles)
        {
            Console.WriteLine("Testing: {0}", String.Join(", ", existingFiles));

            var fileMock = new Mock<FileBase>();
            var fileSystemMock = new Mock<IFileSystem>();

            foreach (var existingFile in existingFiles)
            {
                fileMock.Setup(f => f.Exists("site\\" + existingFile)).Returns(true);
            }

            fileSystemMock.Setup(f => f.File).Returns(fileMock.Object);
            FileSystemHelpers.Instance = fileSystemMock.Object;

            bool looksLikeNodeResult = NodeSiteEnabler.LooksLikeNode("site");
            Assert.Equal(looksLikeNodeExpectedResult, looksLikeNodeResult);
        }
    }
}
