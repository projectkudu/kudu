using System;
using System.Configuration;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Functions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Core.Test.Functions
{
    public class FunctionManagerTests
    {
        private IFileSystem MockFileSystem(string directory, string[] files)
        {
            var fileSystemMock = new Mock<IFileSystem>();
            var fileBaseMock = new Mock<FileBase>();
            var directoryBaseMock = new Mock<DirectoryBase>();

            fileSystemMock.SetupGet(fs => fs.File)
                      .Returns(fileBaseMock.Object);
            fileSystemMock.SetupGet(fs => fs.Directory)
                      .Returns(directoryBaseMock.Object);
            string[] fullPaths = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                fullPaths[i] = Path.Combine(directory, files[i]);
            }
            fileBaseMock.Setup(f => f.Exists(It.IsIn<String>(fullPaths))).Returns(true);
            fileBaseMock.Setup(f => f.Exists(It.IsNotIn<String>(fullPaths))).Returns(false);
            directoryBaseMock.Setup(d => d.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
                        .Returns(fullPaths); // technically this returns with flag SearchOption.AllDirectory
            return fileSystemMock.Object;
        }

        private void runDeterminePrimaryScriptFile(string expect, string jObjectStr, string dir)
        {
            JObject functionConfig = JObject.Parse(jObjectStr);
            var traceFactoryMock = new Mock<ITraceFactory>();
            traceFactoryMock.Setup(tf => tf.GetTracer()).Returns(NullTracer.Instance);

            var functionManager = new FunctionManager(new Mock<IEnvironment>().Object, traceFactoryMock.Object);
            if (string.IsNullOrEmpty(expect))
            {
                Assert.Throws<ConfigurationErrorsException>(() => functionManager.DeterminePrimaryScriptFile(functionConfig, dir));
            }
            else
            {
                Assert.Equal(expect, functionManager.DeterminePrimaryScriptFile(functionConfig, dir), StringComparer.OrdinalIgnoreCase);
            }
        }

        [Theory]
        // missing script files
        [InlineData("", new[] { "function.json" })]
        // unable to determine primary
        [InlineData("", new[] { "function.json", "randomFileA.txt", "randomFileB.txt" })]
        // only one file left in function directory
        [InlineData(@"c:\functions\functionScript.py", new[] { "function.json", "functionScript.py" })]
        // with datafiles in function directory
        [InlineData(@"c:\functions\index.js", new[] { "function.json", "index.js", "test1.dat", "test2.dat" })]
        [InlineData(@"c:\functions\run.csx", new[] { "function.json", "run.csx", "test.dat" })]
        public void DeterminePrimaryScriptFileTests(string expect, string[] files)
        {
            var dir = @"c:\functions";
            var functionConfigStr = "{}";
            FileSystemHelpers.Instance = MockFileSystem(dir, files);
            runDeterminePrimaryScriptFile(expect, functionConfigStr, dir);

        }

        [Theory]
        // https://github.com/projectkudu/kudu/issues/2334
        [InlineData("{\"scriptFile\": \"subDirectory\\\\compiled.dll\"}", @"c:\functions\subDirectory\compiled.dll")]
        // cannot find script file specified
        [InlineData("{\"scriptFile\": \"random.text\"}", "")]
        public void DeterminePrimaryScriptFileWithConfigTests(string functionConfigStr, string expect)
        {
            var dir = @"c:\functions";
            FileSystemHelpers.Instance = MockFileSystem(@"c:\functions", new string[] { "function.json", @"subDirectory\compiled.dll" });
            runDeterminePrimaryScriptFile(expect, functionConfigStr, dir);
        }
    }
}
