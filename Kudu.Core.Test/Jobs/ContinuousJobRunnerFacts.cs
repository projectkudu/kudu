using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class ContinuousJobRunnerFacts : IDisposable
    {
        private ContinuousJob _job;
        private ContinuousJobRunner _runner;
        private TestEnvironment _environment;
        private string _logFilePath;
        private string _jobsDataPath;

        public ContinuousJobRunnerFacts()
        {
            _jobsDataPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_jobsDataPath);

            _job = new ContinuousJob
            {
                Name = "testjob",
                JobBinariesRootPath = @"c:\test\data\continuous\testjob"
            };
            _environment = new TestEnvironment
            {
                TempPath = @"c:\temp",
                JobsBinariesPath = @"c:\test\data\continuous\testjob",
                JobsDataPath = _jobsDataPath,
                DataPath = @"c:\test\data"
            };

            string jobDirectory = Path.Combine(_environment.JobsDataPath, "continuous", _job.Name);
            _logFilePath = Path.Combine(jobDirectory, "job_log.txt");

            MockDeploymentSettingsManager mockSettingsManager = new MockDeploymentSettingsManager();
            Mock<ITraceFactory> mockTraceFactory = new Mock<ITraceFactory>(MockBehavior.Strict);
            Mock<IAnalytics> mockAnalytics = new Mock<IAnalytics>();

            Mock<ITracer> mockTracer = new Mock<ITracer>(MockBehavior.Strict);
            mockTracer.Setup(p => p.Trace(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()));
            mockTraceFactory.Setup(p => p.GetTracer()).Returns(mockTracer.Object);

            _runner = new ContinuousJobRunner(_job, _environment.JobsBinariesPath, _environment, mockSettingsManager, mockTraceFactory.Object, mockAnalytics.Object);

            FileSystemHelpers.DeleteFileSafe(_logFilePath);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("1", false)]
        [InlineData("0", true)]
        public void AlwaysOnNotEnabled_ReturnsExpectedValue(string alwaysOnEnvironmentValue, bool expected)
        {
            if (alwaysOnEnvironmentValue != null)
            {
                System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, alwaysOnEnvironmentValue);
            }

            Assert.Equal(expected, ContinuousJobRunner.AlwaysOnNotEnabled());

            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, null);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckAlwaysOn_Continuous_LogsWarning_WhenAlwaysOnNotEnabled(bool alwaysOnEnabled)
        {
            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, alwaysOnEnabled ? "1" : "0");

            _runner.CheckAlwaysOn();

            if (alwaysOnEnabled)
            {
                Assert.False(File.Exists(_logFilePath));
            }
            else
            {
                VerifyAlwaysOnWarningWritten();
            }

            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, null);
        }

        [Fact]
        public void CheckJobType_Continuous_JobTypeIsNonSdk_ByDefault()
        {
            Assert.Equal("continuous", _runner.GetJobType());
        }

        [Fact]
        public void CheckAlwaysOn_OnlyLogsOncePerLogFile()
        {
            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, "0");

            _runner.CheckAlwaysOn();
            _runner.CheckAlwaysOn();
            _runner.CheckAlwaysOn();

            VerifyAlwaysOnWarningWritten();

            // when the log file is rolled, we expect the new log file to
            // contain the warning
            FileSystemHelpers.DeleteFileSafe(_logFilePath);
            _runner.OnLogFileRolled();
            _runner.CheckAlwaysOn();
            _runner.CheckAlwaysOn();

            VerifyAlwaysOnWarningWritten();

            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, null);
        }

        [Fact]
        public void RefreshJob_Continuous_LogsWarning_WhenAlwaysOnNotEnabled()
        {
            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, "0");
            Assert.True(ContinuousJobRunner.AlwaysOnNotEnabled());

            JobSettings settings = new JobSettings();
            _runner.RefreshJob(_job, settings, false);

            VerifyAlwaysOnWarningWritten();

            System.Environment.SetEnvironmentVariable(ContinuousJobRunner.WebsiteSCMAlwaysOnEnabledKey, null);
        }

        private void VerifyAlwaysOnWarningWritten()
        {
            IEnumerable<string> logEntries = File.ReadLines(_logFilePath).ToArray();

            string expectedWarning = "SYS WARN] " + Resources.Warning_EnableAlwaysOn;
            Assert.Equal(1, logEntries.Count(p => p.Trim().EndsWith(expectedWarning)));
        }

        public void Dispose()
        {
            if (_runner != null)
            {
                _runner.Dispose();
            }

            if (Directory.Exists(_jobsDataPath))
            {
                Directory.Delete(_jobsDataPath, recursive: true);
            }
        }
    }
}
