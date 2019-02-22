using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Xunit;

namespace Kudu.Core.Test
{
    public class ProcessExtensionsFacts
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("NotSCMAppPool", false)]
        [InlineData("~1SCMAppPool", true)]
        [InlineData("~1SCMAppPool", true)]
        public void GetIsScmSite_ReturnsExpectedResult(string appPoolId, bool expectedValue)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            if (appPoolId != null)
            {
                environment[WellKnownEnvironmentVariables.ApplicationPoolId] = appPoolId;
            }

            Assert.Equal(expectedValue, ProcessExtensions.GetIsScmSite(environment));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", true)]
        [InlineData("TestWebJob", true)]
        public void GetIsWebJob_ReturnsExpectedResult(string webJobName, bool expectedValue)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            if (webJobName != null)
            {
                environment[WellKnownEnvironmentVariables.WebJobsName] = webJobName;
            }

            Assert.Equal(expectedValue, ProcessExtensions.GetIsWebJob(environment));
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("TestJob", null, null)]
        [InlineData(null, "Continuous", null)]
        [InlineData("TestJob", "Continuous", "WebJob: TestJob, Type: Continuous")]
        [InlineData("", "", "WebJob: , Type: ")]
        public void GetDescription_WebJob_ReturnsExpectedResult(string webJobName, string webJobType, string expectedValue)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            if (webJobName != null)
            {
                environment[WellKnownEnvironmentVariables.WebJobsName] = webJobName;
            }
            if (webJobType != null)
            {
                environment[WellKnownEnvironmentVariables.WebJobsType] = webJobType;
            }

            Assert.Equal(expectedValue, ProcessExtensions.GetDescription(environment));
        }

        [Fact]
        public void SnapshotAndDump_CreatesAMinidump()
        {
            const int minimumReasonableDmpFileLength = 256 * 1024; // 256 KB

            // MINIDUMP_HEADER constants from minidumpapiset.h
            const uint MINIDUMP_SIGNATURE = 0x504d444d; // 'PMDM'
            const ushort MINIDUMP_VERSION = 42899;
            const int timeStampToleranceInSeconds = 30;

            var process = Process.GetCurrentProcess();
            var dmpFile = Path.GetTempFileName();
            try
            {
                var utcTimeBeforeSnapshot = DateTime.UtcNow;

                ProcessExtensions.SnapshotAndDump(process, dmpFile, MINIDUMP_TYPE.Normal);
                var dmpFileInfo = new FileInfo(dmpFile);
                Assert.True(dmpFileInfo.Exists);
                Assert.True(dmpFileInfo.Length > minimumReasonableDmpFileLength);

                // Check for sensible values in the minidump header.
                using (var reader = new BinaryReader(File.OpenRead(dmpFile)))
                {
                    var signature = reader.ReadUInt32();
                    Assert.Equal(MINIDUMP_SIGNATURE, signature);

                    var version = reader.ReadUInt32();
                    Assert.Equal(MINIDUMP_VERSION, (ushort)version);

                    var numberOfStreams = reader.ReadUInt32();
                    Assert.True(numberOfStreams > 0 && numberOfStreams < 40);

                    var streamDirectoryRva = reader.ReadUInt32();
                    var checkSum = reader.ReadUInt32();

                    var timeDateStamp = reader.ReadUInt32();
                    // Convert from time_t to FILETIME
                    var filetimeUtc = timeDateStamp * 10000000L + 116444736000000000L;
                    var dmpTimeStamp = DateTime.FromFileTimeUtc(filetimeUtc);
                    Assert.True(dmpTimeStamp - utcTimeBeforeSnapshot < TimeSpan.FromSeconds(timeStampToleranceInSeconds));

                    var flags = reader.ReadUInt64();
                    Assert.Equal((MINIDUMP_TYPE)flags, MINIDUMP_TYPE.Normal | MINIDUMP_TYPE.IgnoreInaccessibleMemory);
                }
            }
            finally
            {
                try
                {
                    File.Delete(dmpFile);
                }
                catch
                {
                }
            }
        }
    }
}
