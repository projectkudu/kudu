using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Deployment;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kudu.Services.Test
{
    public class PushDeploymentControllerFacts
    {
        [Fact]
        public async Task Go()
        {
            var siteRoot = @"x:\vdir0\site";

            FileSystemHelpers.Instance = GetFileSystem(siteRoot).Object;

            var environment = GetEnvironment(siteRoot);

            var opLock = new Mock<IOperationLock>();
            opLock.Setup(f => f.Lock(It.IsAny<string>())).Returns(true);

            var repoFactory = new Mock<IRepositoryFactory>();
            repoFactory.Setup(f => f.GetZipDeployRepository(It.IsAny<string>())).Returns<string>(path =>
                new NullRepository(path, Mock.Of<ITraceFactory>(), doBuildDuringDeploymentByDefault: false));

            var controller = new PushDeploymentController(
                Mock.Of<IDeploymentManager>(),
                Mock.Of<ITracer>(),
                opLock.Object,
                environment.Object,
                Mock.Of<IRepositoryFactory>());

            controller.Request = GetRequest();

            var response = await controller.ZipPushDeploy();
        }

        private static HttpRequestMessage GetRequest()
        {
            var request = new HttpRequestMessage();
            return request;
        }

        private Mock<IEnvironment> GetEnvironment(string siteRoot)
        {
            string deploymentsPath = Path.Combine(siteRoot, Constants.DeploymentCachePath);
            var env = new Mock<IEnvironment>(MockBehavior.Strict);
            env.SetupGet(e => e.DeploymentsPath).Returns(deploymentsPath);
            env.SetupGet(e => e.TempPath).Returns(@"x:\temp");
            return env;
        }

        private Mock<IFileSystem> GetFileSystem(string siteRoot, params DateTime[] writeTimeUtcs)
        {
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var markerFile = Path.Combine(siteRoot, Constants.DeploymentCachePath, "pending");
            var index = 0;
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(fileBase.Object);
            fileBase.Setup(f => f.Exists(markerFile))
                    .Returns(() => files.ContainsKey(markerFile));
            fileBase.Setup(f => f.WriteAllText(markerFile, It.IsAny<string>()))
                    .Callback((string path, string contents) => files[path] = contents);
            fileBase.Setup(f => f.GetLastWriteTimeUtc(markerFile))
                    .Returns(() => index < writeTimeUtcs.Length ? writeTimeUtcs[index++] : DateTime.MinValue);
            return fileSystem;
        }
    }
}
