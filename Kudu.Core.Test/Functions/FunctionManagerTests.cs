using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
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

        [Fact]
        public void DeterminePrimaryScriptFileMissingTest()
        {
            JObject functionConfig = new JObject()
            {
                { "scriptFile", "QUEUETriggER.py" }
            };
            var fileSystemMock = new Mock<IFileSystem>();
            var fileBaseMock = new Mock<FileBase>();

            fileSystemMock.SetupGet(fs => fs.File)
                      .Returns(fileBaseMock.Object);
            fileBaseMock.Setup(f => f.Exists(@"c:\functions\QUEUETriggER.py"))
                        .Returns(false);
            FileSystemHelpers.Instance = fileSystemMock.Object;

            var traceFactoryMock = new Mock<ITraceFactory>();
            traceFactoryMock.Setup(tf => tf.GetTracer()).Returns(NullTracer.Instance);

            var functionManager = new FunctionManager(new Mock<IEnvironment>().Object, traceFactoryMock.Object);
            Assert.Throws<ConfigurationErrorsException>(() => functionManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions"));
        }
    }
}
