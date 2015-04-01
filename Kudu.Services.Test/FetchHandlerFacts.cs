using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class FetchHandlerFacts
    {
        [Theory]
        [MemberData("Scenarios")]
        public async Task FetchHandlerBasicTests(IScenario scenario)
        {
            // Arrange
            var siteRoot = @"x:\vdir0\site";
            var deploymentManager = new MockDeploymentManager();
            var fileSystem = GetFileSystem(siteRoot, scenario.WriteTimeUTCs.ToArray());
            var environment = GetEnvironment(siteRoot);
            var serviceHookHandler = GetServiceHookHandler();
            var repositoryFactory = GetRepositoryFactory();
            var handler = CreateFetchHandler(deploymentManager: deploymentManager,
                                             fileSystem: fileSystem.Object,
                                             serviceHookHandler: serviceHookHandler.Object,
                                             repositoryFactory: repositoryFactory.Object,
                                             environment: environment.Object);

            // Test
            await handler.PerformDeployment(new DeploymentInfo
            {
                IsReusable = scenario.IsReusable,
                Handler = serviceHookHandler.Object,
                TargetChangeset = GetChangeSet()
            });

            // Assert
            Assert.Equal(scenario.DeployCount, deploymentManager.DeployCount);
        }

        public static IEnumerable<object[]> Scenarios
        {
            get
            {
                yield return new object[] { new BasicScenario() };
                yield return new object[] { new ConcurrentScenario() };
                yield return new object[] { new NotReusableConcurrentScenario() };
            }
        }

        public interface IScenario
        {
            bool IsReusable { get; }
            IEnumerable<DateTime> WriteTimeUTCs { get; }
            int DeployCount { get; }
        }

        public class BasicScenario : IScenario
        {
            public IEnumerable<DateTime> WriteTimeUTCs { get { return new DateTime[0]; } }
            public bool IsReusable { get { return true; } }
            public int DeployCount { get { return 1; } }
        }

        public class ConcurrentScenario : IScenario
        {
            public IEnumerable<DateTime> WriteTimeUTCs { get { return new[] { DateTime.Now.AddSeconds(10), DateTime.Now.AddSeconds(20) }; } }
            public bool IsReusable { get { return true; } }
            public int DeployCount { get { return 3; } }
        }

        public class NotReusableConcurrentScenario : IScenario
        {
            public IEnumerable<DateTime> WriteTimeUTCs { get { return new[] { DateTime.Now.AddSeconds(10), DateTime.Now.AddSeconds(20) }; } }
            public bool IsReusable { get { return false; } }
            public int DeployCount { get { return 1; } }
        }

        private FetchHandler CreateFetchHandler(ITracer tracer = null,
                                                IDeploymentManager deploymentManager = null,
                                                IDeploymentSettingsManager settings = null,
                                                IDeploymentStatusManager status = null,
                                                IOperationLock deploymentLock = null,
                                                IEnvironment environment = null,
                                                IServiceHookHandler serviceHookHandler = null,
                                                IRepositoryFactory repositoryFactory = null,
                                                IFileSystem fileSystem = null,
                                                IAutoSwapHandler autoSwapHandler = null)
        {
            FileSystemHelpers.Instance = fileSystem ?? Mock.Of<IFileSystem>();
            return new FetchHandler(tracer ?? Mock.Of<ITracer>(),
                                    deploymentManager ?? Mock.Of<IDeploymentManager>(),
                                    settings ?? Mock.Of<IDeploymentSettingsManager>(),
                                    status ?? Mock.Of<IDeploymentStatusManager>(),
                                    deploymentLock ?? Mock.Of<IOperationLock>(),
                                    environment ?? Mock.Of<IEnvironment>(),
                                    new[] { serviceHookHandler ?? Mock.Of<IServiceHookHandler>() },
                                    repositoryFactory ?? Mock.Of<IRepositoryFactory>(),
                                    autoSwapHandler ?? Mock.Of<IAutoSwapHandler>());
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

        private Mock<IServiceHookHandler> GetServiceHookHandler()
        {
            var handler = new Mock<IServiceHookHandler>();
            handler.Setup(h => h.Fetch(It.IsAny<IRepository>(), It.IsAny<DeploymentInfo>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                   .Returns(Task.FromResult(0));
            return handler;
        }

        private static ChangeSet GetChangeSet()
        {
            return new ChangeSet(Guid.NewGuid().ToString(), null, null, null, DateTimeOffset.Now);
        }

        private Mock<IRepositoryFactory> GetRepositoryFactory()
        {
            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            repositoryFactory.Setup(f => f.EnsureRepository(It.IsAny<RepositoryType>()))
                             .Returns(() => Mock.Of<IRepository>());
            return repositoryFactory;
        }

        private Mock<IEnvironment> GetEnvironment(string siteRoot)
        {
            string deploymentsPath = Path.Combine(siteRoot, Constants.DeploymentCachePath);
            var env = new Mock<IEnvironment>(MockBehavior.Strict);
            env.SetupGet(e => e.DeploymentsPath)
               .Returns(deploymentsPath);
            return env;
        }

        public class MockDeploymentManager : IDeploymentManager
        {
            public int DeployCount
            {
                get;
                private set;
            }

            public IEnumerable<DeployResult> GetResults()
            {
                throw new NotImplementedException();
            }

            public DeployResult GetResult(string id)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<LogEntry> GetLogEntries(string id)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<LogEntry> GetLogEntryDetails(string id, string logId)
            {
                throw new NotImplementedException();
            }

            public void Delete(string id)
            {
                throw new NotImplementedException();
            }

            public Task DeployAsync(IRepository repository, ChangeSet changeSet, string deployer, bool clean, bool needFileUpdate = true)
            {
                ++DeployCount;
                return Task.FromResult(1);
            }

            public IDisposable CreateTemporaryDeployment(string statusText, out ChangeSet tempChangeSet, ChangeSet changeset = null, string deployedBy = null)
            {
                tempChangeSet = GetChangeSet();
                return new NoopDisposable();
            }

            public ILogger GetLogger(string id)
            {
                return Mock.Of<ILogger>();
            }

            public string GetDeploymentScriptContent()
            {
                return null;
            }

            public class NoopDisposable : IDisposable
            {
                public void Dispose()
                {
                    // no-op
                }
            }
        }
    }
}