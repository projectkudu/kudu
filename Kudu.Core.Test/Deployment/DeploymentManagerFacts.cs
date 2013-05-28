using System;
using System.IO.Abstractions;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Hooks;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Deployment
{
    public class DeploymentManagerFacts
    {
        [Fact]
        public void GetOrCreateStatusFileCreatesFileIfItDoesNotAlreadyExist()
        {
            // Arrange
            var changeSet = new ChangeSet("test-changeset-id", "author", "author@email.com", "commit message", DateTimeOffset.UtcNow);
            var status = new Mock<IDeploymentStatusManager>();
            var statusFile = new TestDeploymentStatusFile();
            status.Setup(s => s.Create("test-changeset-id")).Returns(statusFile).Verifiable();
            var deploymentManager = CreateDeploymentManager(status: status.Object);
            var tracer = Mock.Of<ITracer>();

            // Act
            deploymentManager.GetOrCreateStatusFile(changeSet, tracer, "test-deployer");

            // Assert
            status.Verify();
            Assert.Equal("test-deployer", statusFile.Deployer);
            Assert.Equal("author", statusFile.Author);
            Assert.Equal("author@email.com", statusFile.AuthorEmail);
            Assert.Equal("commit message", statusFile.Message);
        }

        [Fact]
        public void GetOrCreateStatusFileUpdatesFileIfItAlreadyExists()
        {
            // Arrange
            var changeSet = new ChangeSet("test-changeset-id", "author", "author@email.com", "commit message", DateTimeOffset.UtcNow);
            var status = new Mock<IDeploymentStatusManager>(MockBehavior.Strict);
            var statusFile = new TestDeploymentStatusFile();
            status.Setup(s => s.Open("test-changeset-id")).Returns(statusFile).Verifiable();
            var deploymentManager = CreateDeploymentManager(status: status.Object);
            var tracer = Mock.Of<ITracer>();

            // Act
            deploymentManager.GetOrCreateStatusFile(changeSet, tracer, "test-deployer");

            // Assert
            status.Verify();
            Assert.Equal("test-deployer", statusFile.Deployer);
            Assert.Equal("author", statusFile.Author);
            Assert.Equal("author@email.com", statusFile.AuthorEmail);
            Assert.Equal("commit message", statusFile.Message);
        }

        public class TestDeploymentStatusFile : IDeploymentStatusFile
        {
            public string Id { get; set; }

            public DeployStatus Status { get; set; }

            public string StatusText { get; set; }

            public string AuthorEmail { get; set; }

            public string Author { get; set; }

            public string Message { get; set; }

            public string Progress { get; set; }

            public string Deployer { get; set; }

            public DateTime ReceivedTime { get; set; }

            public DateTime StartTime { get; set; }

            public DateTime? EndTime { get; set; }

            public DateTime? LastSuccessEndTime { get; set; }

            public bool Complete { get; set; }

            public bool IsTemporary { get; set; }

            public bool IsReadOnly { get; set; }

            public void Save()
            {
                // Do nothing.
            }
        }

        private static DeploymentManager CreateDeploymentManager(
                                 ISiteBuilderFactory builderFactory = null,
                                 IEnvironment environment = null,
                                 IFileSystem fileSystem = null,
                                 ITraceFactory traceFactory = null,
                                 IDeploymentSettingsManager settings = null,
                                 IDeploymentStatusManager status = null,
                                 IOperationLock deploymentLock = null,
                                 ILogger globalLogger = null,
                                 IWebHooksManager hooksManager = null)
        {
            builderFactory = builderFactory ?? Mock.Of<ISiteBuilderFactory>();
            environment = environment ?? Mock.Of<IEnvironment>();
            fileSystem = fileSystem ?? Mock.Of<IFileSystem>();
            traceFactory = traceFactory ?? Mock.Of<ITraceFactory>();
            settings = settings ?? Mock.Of<IDeploymentSettingsManager>();
            status = status ?? Mock.Of<IDeploymentStatusManager>();
            deploymentLock = deploymentLock ?? Mock.Of<IOperationLock>();
            globalLogger = globalLogger ?? Mock.Of<ILogger>();

            return new DeploymentManager(builderFactory, environment, fileSystem, traceFactory, settings, status, deploymentLock, globalLogger, hooksManager);
        }
    }
}