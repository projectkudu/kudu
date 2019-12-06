using Kudu.Core.Jobs;
using System;
using System.IO;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class JobsManagerBaseFacts : IDisposable
    {
        private const string testJobDataDir = @"c:\test\data\continuous";
        private string _jobName;
        private string _jobDataDir;

        public JobsManagerBaseFacts()
        {
            _jobName = Path.GetRandomFileName();
            _jobDataDir = Path.Combine(testJobDataDir, _jobName);
            Directory.CreateDirectory(_jobDataDir);
        }

        [Fact]
        public void JobsManagerBase_IsUsingSdk_WhenMarkerPresents()
        {
            string markerPath = Path.Combine(_jobDataDir, "webjobssdk.marker");
            File.Create(markerPath).Close();
            Assert.True(JobsManagerBase.IsUsingSdk(_jobDataDir));
        }

        [Fact]
        public void JobsManagerBase_IsNotUsingSdk_WhenMarkerAbsents()
        {
            Assert.False(JobsManagerBase.IsUsingSdk(_jobDataDir));
        }

        public void Dispose()
        {
            if (Directory.Exists(_jobDataDir))
            {
                Directory.Delete(_jobDataDir, recursive: true);
            }
        }
    }
}
