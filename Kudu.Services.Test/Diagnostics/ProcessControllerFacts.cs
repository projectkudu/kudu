using System;
using System.Collections.Generic;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Diagnostics;
using Kudu.Services.Performance;
using Moq;
using Xunit;

namespace Kudu.Services.Test.Diagnostics
{
    public class ProcessControllerFacts : IDisposable
    {
        private Mock<ITracer> _tracerMock;
        private Mock<IEnvironment> _environmentMock;
        private Mock<IDeploymentSettingsManager> _deploymentSettingsManagerMock;
        private ProcessController _controller;

        public ProcessControllerFacts()
        {
            _tracerMock = new Mock<ITracer>(MockBehavior.Strict);
            _environmentMock = new Mock<IEnvironment>(MockBehavior.Strict);
            _deploymentSettingsManagerMock = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            _controller = new ProcessController(_tracerMock.Object, _environmentMock.Object, _deploymentSettingsManagerMock.Object);
        }

        [Fact]
        public void SetEnvironmentInfo_NotWebJob_ReturnsExpectedResults()
        {
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            ProcessInfo processInfo = new ProcessInfo
            {
                EnvironmentVariables = environmentVariables
            };
            _controller.SetEnvironmentInfo(processInfo);

            Assert.False(processInfo.IsScmSite);
            Assert.False(processInfo.IsWebJob);
            Assert.Null(processInfo.Description);
        }

        [Fact]
        public void SetEnvironmentInfo_WebJob_ReturnsExpectedResults()
        {
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables.Add("APP_POOL_ID", "~1SCMPool");
            environmentVariables.Add("WEBJOBS_NAME", "TestJob");
            environmentVariables.Add("WEBJOBS_TYPE", "Continuous");
            ProcessInfo processInfo = new ProcessInfo
            {
                EnvironmentVariables = environmentVariables
            };
            _controller.SetEnvironmentInfo(processInfo);

            Assert.True(processInfo.IsScmSite);
            Assert.True(processInfo.IsWebJob);
            Assert.Equal("WebJob: TestJob, Type: Continuous", processInfo.Description);
        }

        public void Dispose()
        {
            if (_controller != null)
            {
                _controller.Dispose();
            }
        }
    }
}
