using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web.SessionState;
using System.Web.UI.WebControls;
using Kudu.Client.Deployment;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
    class DeploymentTestHelper
    {
        public static TestFile CreateRandomTestFile()
        {
            var fileValues = Guid.NewGuid().ToString("N");
            return new TestFile { Content = fileValues, Filename = fileValues };
        }

        public static TestFile[] CreateRandomFilesForZip(int numFiles)
        {
            return Enumerable.Range(0, numFiles)
                .Select(i => Guid.NewGuid().ToString("N"))
                .Select(s => new TestFile { Content = s, Filename = s })
                .ToArray();
        }

        public static async Task WaitForDeploymentCompletionAsync(ApplicationManager appManager, string deployer)
        {
            DeployResult result;
            do
            {
                result = await appManager.DeploymentManager.GetResultAsync("latest");
                Assert.Equal(deployer, result.Deployer);
                await Task.Delay(TimeSpan.FromSeconds(2));
            } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));
        }

        public static Stream CreateZipStream(TestFile[] files)
        {
            var s = new MemoryStream();
            using (var archive = new ZipArchive(s, ZipArchiveMode.Update, true))
            {
                foreach (var file in files)
                {
                    var entry = archive.CreateEntry(file.Filename);

                    using (var w = new StreamWriter(entry.Open()))
                    {
                        w.Write(file.Content);
                    }

                    if (file.ModifiedTime.HasValue)
                    {
                        entry.LastWriteTime = file.ModifiedTime.Value;
                    }
                }
            }

            s.Position = 0;
            return s;
        }

        public static Stream CreateFileStream(TestFile file)
        {
            var s = new MemoryStream();
            using (var sw = new StreamWriter(s, Encoding.Default, file.Content.Length, true))
            {
                sw.Write(file.Content);
            }
            s.Position = 0;
            return s;
        }

        public static async Task AssertSuccessfulDeploymentByFilenames(ApplicationManager appManager, IEnumerable<string> expectedFileNames, string path = null)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);

            var entries = await appManager.VfsManager.ListAsync(path);
            var observedFileNames = entries.Select(e => e.Name);

            var expectedFileNameSet = new HashSet<string>(expectedFileNames);
            Assert.True(expectedFileNameSet.SetEquals(entries.Select(e => e.Name)), string.Join(",", expectedFileNameSet) + " != " + string.Join(",", entries.Select(e => e.Name)));
        }

        // Deploy files to all directories of interest
        // Useful helper function for testing 'clean' deploy scenarios
        public static string DeployRandomFilesEverywhere(
            ApplicationManager appManager)
        {
            TestTracer.Trace("Deploying random files everywhere");

            var testFile = DeploymentTestHelper.CreateRandomTestFile();

            appManager.VfsManager.WriteAllText($"site/scripts/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/libs/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/wwwroot/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/wwwroot/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/wwwroot/webapps/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/wwwroot/webapps/ROOT/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/wwwroot/webapps/ROOT2/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/test/{testFile.Filename}", testFile.Content);
            appManager.VfsManager.WriteAllText($"site/test/test2/{testFile.Filename}", testFile.Content);

            return testFile.Content;
        }
    }
}
