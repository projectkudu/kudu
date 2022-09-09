using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
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

        private void RunDeterminePrimaryScriptFileFunc(string expect, string jObjectStr, string dir)
        {
            JObject functionConfig = JObject.Parse(jObjectStr);

            var traceFactoryMock = new Mock<ITraceFactory>();
            traceFactoryMock.Setup(tf => tf.GetTracer()).Returns(NullTracer.Instance);

            var functionManager = new FunctionManager(new Mock<IEnvironment>().Object, traceFactoryMock.Object);
            string[] functionFiles = FileSystemHelpers.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                   .Where(p => !String.Equals(Path.GetFileName(p), Constants.FunctionsConfigFile, StringComparison.OrdinalIgnoreCase))
                   .ToArray();
            string scriptFile = (string)functionConfig["scriptFile"];
            string scriptPath = string.IsNullOrEmpty(scriptFile) ? null : Path.Combine(dir, scriptFile);

            if ((string.IsNullOrEmpty(scriptFile) && functionFiles.Length == 0) || expect == null)
            {
                Assert.Null(functionManager.DeterminePrimaryScriptFile((string)functionConfig["scriptFile"], dir));
                return;
            }
            if (!string.IsNullOrEmpty(scriptPath) && !FileSystemHelpers.FileExists(scriptPath))
            {
                Assert.Throws<ConfigurationErrorsException>(() => functionManager.DeterminePrimaryScriptFile((string)functionConfig["scriptFile"], dir));
            }
            else
            {
                Assert.Equal(expect, functionManager.DeterminePrimaryScriptFile((string)functionConfig["scriptFile"], dir), StringComparer.OrdinalIgnoreCase);
            }
        }

        [Theory]
        // missing script files
        [InlineData(null, new[] { "function.json" }, "{}")]
        // unable to determine primary
        [InlineData(null, new[] { "function.json", "randomFileA.txt", "randomFileB.txt" }, "{}")]
        // only one file left in function directory
        [InlineData(@"c:\functions\functionScript.py", new[] { "function.json", "functionScript.py" }, "{}")]
        // with datafiles in function directory
        [InlineData(@"c:\functions\index.js", new[] { "function.json", "index.js", "test1.dat", "test2.dat" }, "{}")]
        [InlineData(@"c:\functions\run.csx", new[] { "function.json", "run.csx", "test.dat" }, "{}")]
        public void DeterminePrimaryScriptFileNotSpecifiedTests(string expect, string[] files, string functionConfigStr)
        {
            var dir = @"c:\functions";
            FileSystemHelpers.Instance = MockFileSystem(dir, files);
            RunDeterminePrimaryScriptFileFunc(expect, functionConfigStr, dir);
        }

        [Theory]
        // https://github.com/projectkudu/kudu/issues/2334
        [InlineData("{\"scriptFile\": \"subDirectory\\\\compiled.dll\"}", @"c:\functions\subDirectory\compiled.dll")]
        // cannot find script file specified
        [InlineData("{\"scriptFile\": \"random.text\"}", "{\"scriptFile\": \"random.text\"}")]
        public void DeterminePrimaryScriptFileSpecifiedTests(string functionConfigStr, string expect)
        {
            var dir = @"c:\functions";
            FileSystemHelpers.Instance = MockFileSystem(@"c:\functions", new string[] { "function.json", @"subDirectory\compiled.dll" });
            RunDeterminePrimaryScriptFileFunc(expect, functionConfigStr, dir);
        }

        [Theory, MemberData("ThrowsIfFunctionVersionMismatchData")]
        public void ThrowsIfFunctionVersionMismatchTests(IEnumerable<string> projectProperties, string functionRuntimeVersion, bool success)
        {
            FunctionAppHelper.FunctionRunTimeVersion = functionRuntimeVersion;
            try
            {
                FunctionAppHelper.ThrowsIfVersionMismatch(projectProperties);
                Assert.True(success, "Expecting not successful");
            }
            catch (InvalidOperationException)
            {
                if (success)
                {
                    throw;
                }
            }
            finally
            {
                FunctionAppHelper.FunctionRunTimeVersion = null;
            }
        }

        public static IEnumerable<object[]> ThrowsIfFunctionVersionMismatchData
        {
            get
            {
                // happy cases
                yield return new object[] { new[] { "v1" }, "~1", true };
                yield return new object[] { new[] { "v1" }, "1", true };
                yield return new object[] { new[] { "v1" }, "1.does.not.matter", true };
                yield return new object[] { new[] { "v2" }, "~2", true };
                yield return new object[] { new[] { "v2" }, "2", true };
                yield return new object[] { new[] { "v2" }, "2.does.not.matter", true };
                yield return new object[] { new[] { "v3" }, "~3", true };
                yield return new object[] { new[] { "v3" }, "3", true };
                yield return new object[] { new[] { "v3" }, "3.does.not.matter", true };
                yield return new object[] { new[] { "v3-preview" }, "~3", true };
                yield return new object[] { new[] { "v3-preview" }, "3", true };
                yield return new object[] { new[] { "v3-preview" }, "3.does.not.matter", true };
                yield return new object[] { new[] { "v3.does.not.matter-preview" }, "~3", true };
                yield return new object[] { new[] { "v3.does.not.matter-preview" }, "3", true };
                yield return new object[] { new[] { "v3.does.not.matter-preview" }, "3.does.not.matter", true };
                yield return new object[] { new[] { "v13" }, "~13", true };
                yield return new object[] { new[] { "v13" }, "13", true };
                yield return new object[] { new[] { "v13" }, "13.does.not.matter", true };

                // unhandled cases
                yield return new object[] { new[] { "v1" }, "Beta", true };
                yield return new object[] { new[] { "v2" }, "Latest", true };
                yield return new object[] { new[] { "vx" }, "~4", true };

                // unhappy cases
                yield return new object[] { new[] { "v1" }, "~2", false };
                yield return new object[] { new[] { "v1" }, "2", false };
                yield return new object[] { new[] { "v1" }, "2.does.not.matter", false };
                yield return new object[] { new[] { "v2" }, "~1", false };
                yield return new object[] { new[] { "v2" }, "1", false };
                yield return new object[] { new[] { "v2" }, "1.does.not.matter", false };
                yield return new object[] { new[] { "v13" }, "~1", false };
                yield return new object[] { new[] { "v13" }, "1", false };
                yield return new object[] { new[] { "v13" }, "1.doesnotmatter", false };
                yield return new object[] { new[] { "v1" }, "~13", false };
                yield return new object[] { new[] { "v1" }, "13", false };
                yield return new object[] { new[] { "v1" }, "13.does.not.matter", false };
            }
        }
    }
}
