using System.IO;
using System.IO.Compression;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Kudu.Core.Deployment;
using System.Net.Http;
using System.Net;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class PushDeploymentTests
    {
        [Fact]
        public Task TestSimpleZipDeployment()
        {
            return ApplicationManager.RunAsync("TestSimpleZipDeployment", async appManager =>
            {
                var filenames = await DeploySimpleZipExpectingSuccess(appManager, 10, doAsync: false);
                await AssertSuccessfulDeployment(appManager, filenames);
            });
        }

        [Fact]
        public Task TestAsyncZipDeployment()
        {
            return ApplicationManager.RunAsync("TestPushDeployment", async appManager =>
            {
                // Big enough to require at least a couple polls for status until success
                var filenames = await DeploySimpleZipExpectingSuccess(appManager, 1000, doAsync: true);

                TestTracer.Trace("Confirming deployment is in progress");

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal("Zip-Push", result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await AssertSuccessfulDeployment(appManager, filenames);
            });
        }

        [Fact]
        public Task TestEmptyZipClearsWwwroot()
        {
            return ApplicationManager.RunAsync("TestPushDeployment", async appManager =>
            {
                var filenames = await DeploySimpleZipExpectingSuccess(appManager, 10, doAsync: false);
                await AssertSuccessfulDeployment(appManager, filenames);
                filenames = await DeploySimpleZipExpectingSuccess(appManager, 0, doAsync: false);
                await AssertSuccessfulDeployment(appManager, filenames);
            });
        }

        [Fact]
        public Task EnsureConflictResultOnSimulatneousAsyncRequest()
        {
            return ApplicationManager.RunAsync("TestPushDeployment", async appManager =>
            {
                // Big enough to run for a few seconds
                var filenames = await DeploySimpleZipExpectingSuccess(appManager, 1000, doAsync: true);

                var response = await DeploySimpleZip(appManager, 10, doAsync: true);

                Assert.Equal(HttpStatusCode.Conflict, response.Item1.StatusCode);

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal("Zip-Push", result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await AssertSuccessfulDeployment(appManager, filenames);
            });
        }

        [Fact]
        public Task EnsureConflictResultOnSimulatneousSyncRequest()
        {
            return ApplicationManager.RunAsync("TestPushDeployment", async appManager =>
            {
                // Big enough to run for a few seconds
                var filenames = await DeploySimpleZipExpectingSuccess(appManager, 1000, doAsync: true);

                var response = await DeploySimpleZip(appManager, 10, doAsync: false);

                Assert.Equal(HttpStatusCode.Conflict, response.Item1.StatusCode);

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal("Zip-Push", result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await AssertSuccessfulDeployment(appManager, filenames);
            });
        }

        private static async Task AssertSuccessfulDeployment(ApplicationManager appManager, HashSet<string> filenames)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var entries = await appManager.VfsWebRootManager.ListAsync(null);
            var deployedFilenames = entries.Select(e => e.Name);

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);
            Assert.Equal("Zip-Push", deployment.Deployer);
            Assert.True(filenames.SetEquals(entries.Select(e => e.Name)));
        }

        private static async Task<Tuple<HttpResponseMessage, HashSet<string>>> DeploySimpleZip(ApplicationManager appManager, int numFiles, bool doAsync)
        {
            var fileCount = numFiles;
            HashSet<string> filenames;
            using (var zipStream = CreateZipOfTextFilesStream(fileCount, out filenames))
            {
                TestTracer.Trace("Push-deploying zip with {0} files", fileCount);
                var response = await appManager.PushDeploymentManager.PushDeployFromStream(zipStream, doAsync);
                return Tuple.Create(response, filenames);
            }
        }

        private static async Task<HashSet<string>> DeploySimpleZipExpectingSuccess(ApplicationManager appManager, int numFiles, bool doAsync)
        {
            var result = await DeploySimpleZip(appManager, numFiles, doAsync);
            result.Item1.EnsureSuccessStatusCode();
            return result.Item2;
        }

        private static Stream CreateZipOfTextFilesStream(int numFiles, out HashSet<string> fileNames)
        {
            var s = new MemoryStream();

            var guids = new HashSet<string>(
                Enumerable.Range(0, numFiles)
                .Select(i => Guid.NewGuid()
                .ToString("N"))
                .ToArray());

            using (var archive = new ZipArchive(s, ZipArchiveMode.Create, true))
            {
                foreach (var guid in guids)
                {
                    using (var entryStream = archive.CreateEntry(guid).Open())
                    using (var w = new StreamWriter(entryStream))
                    {
                        w.Write(guid);
                    }
                }
            }

            fileNames = guids;
            s.Position = 0;
            return s;
        }
    }
}
