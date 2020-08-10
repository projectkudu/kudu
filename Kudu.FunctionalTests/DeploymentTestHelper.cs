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


        public class TestFile
        {
            public string Filename;
            public string Content;
            public DateTime? ModifiedTime;
        }
    }
}
