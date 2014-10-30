using System;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Deployment.Generator.Test
{
    public class PythonSiteEnablerTests
    {
        [Theory]
        [InlineData(true, new string[] { "requirements.txt" })]
        [InlineData(true, new string[] { "requirements.txt", "default.asp" })]
        [InlineData(true, new string[] { "requirements.txt", "site.aspx" })]
        [InlineData(false, new string[0])]
        [InlineData(false, new string[] { "index.php" })]
        [InlineData(false, new string[] { "site.aspx" })]
        [InlineData(false, new string[] { "site.aspx", "index2.aspx" })]
        [InlineData(false, new string[] { "server.js" })]
        [InlineData(false, new string[] { "app.js" })]
        [InlineData(false, new string[] { "package.json" })]
        public void TestLooksLikePython(bool looksLikePythonExpectedResult, string[] existingFiles)
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

            bool looksLikePythonResult = PythonSiteEnabler.LooksLikePython("site");
            Assert.Equal(looksLikePythonExpectedResult, looksLikePythonResult);
        }
    }
}
