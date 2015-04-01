using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using Kudu.Core.Infrastructure;
using Moq;
using Xunit;

namespace Kudu.Core.Deployment.Generator.Test
{
    public class PythonSiteEnablerTests
    {
        [Theory]
        [InlineData(false, new string[] { "requirements.txt" }, null)]
        [InlineData(true, new string[] { "requirements.txt", "app.py" }, null)]
        [InlineData(true, new string[] { "requirements.txt", "runtime.txt" }, "python-3.4.1")]
        [InlineData(false, new string[] { "requirements.txt", "runtime.txt" }, "i run all the time")]
        [InlineData(false, new string[] { "requirements.txt", "default.asp" }, null)]
        [InlineData(false, new string[] { "requirements.txt", "site.aspx" }, null)]
        [InlineData(false, new string[0], null)]
        [InlineData(false, new string[] { "index.php" }, null)]
        [InlineData(false, new string[] { "site.aspx" }, null)]
        [InlineData(false, new string[] { "site.aspx", "index2.aspx" }, null)]
        [InlineData(false, new string[] { "server.js" }, null)]
        [InlineData(false, new string[] { "app.js" }, null)]
        [InlineData(false, new string[] { "package.json" }, null)]
        public void TestLooksLikePython(bool looksLikePythonExpectedResult, string[] existingFiles, string runtimeTxtBytes)
        {
            Console.WriteLine("Testing: {0}", String.Join(", ", existingFiles));

            var directoryMock = new Mock<DirectoryBase>();
            var fileMock = new Mock<FileBase>();
            var fileSystemMock = new Mock<IFileSystem>();

            directoryMock.Setup(d => d.GetFiles("site", "*.py")).Returns(existingFiles.Where(f => f.EndsWith(".py")).ToArray());

            foreach (var existingFile in existingFiles)
            {
                fileMock.Setup(f => f.Exists("site\\" + existingFile)).Returns(true);
            }

            if (runtimeTxtBytes != null)
            {
                fileMock.Setup(f => f.Open("site\\runtime.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)).Returns(new System.IO.MemoryStream(Encoding.ASCII.GetBytes(runtimeTxtBytes)));
            }

            fileSystemMock.Setup(f => f.Directory).Returns(directoryMock.Object);
            fileSystemMock.Setup(f => f.File).Returns(fileMock.Object);
            FileSystemHelpers.Instance = fileSystemMock.Object;

            bool looksLikePythonResult = PythonSiteEnabler.LooksLikePython("site");
            Assert.Equal(looksLikePythonExpectedResult, looksLikePythonResult);
        }
    }
}
