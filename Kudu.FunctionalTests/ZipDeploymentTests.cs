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
using Kudu.Contracts.Settings;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class ZipDeploymentTests
    {
        [Fact]
        public Task TestSimpleZipDeployment()
        {
            return ApplicationManager.RunAsync("TestSimpleZipDeployment", async appManager =>
            {
                var files = CreateRandomFilesForZip(10);
                var response = await DeployZip(appManager, files, doAsync: false);
                response.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());
            });
        }

        [Fact]
        public Task TestAsyncZipDeployment()
        {
            return ApplicationManager.RunAsync("TestAsyncZipDeployment", async appManager =>
            {
                // Big enough to require at least a couple polls for status until success
                var files = CreateRandomFilesForZip(1000);
                var response = await DeployZip(appManager, files, doAsync: true);
                response.EnsureSuccessStatusCode();

                TestTracer.Trace("Confirming deployment is in progress");

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal("Zip-Push", result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());
            });
        }

        [Fact]
        public Task TestEmptyZipClearsWwwroot()
        {
            return ApplicationManager.RunAsync("TestEmptyZipClearsWwwroot", async appManager =>
            {
                var files = CreateRandomFilesForZip(10);
                var response = await DeployZip(appManager, files, doAsync: false);
                response.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());

                var empty = new FileForZip[0];
                var response2 = await DeployZip(appManager, empty, doAsync: false);
                await AssertSuccessfulDeploymentByFilenames(appManager, new string[0]);
            });
        }

        [Fact]
        public Task EnsureConflictResultOnSimultaneousAsyncRequest()
        {
            return ApplicationManager.RunAsync("EnsureConflictResultOnSimultaneousAsyncRequest", async appManager =>
            {
                // Big enough to run for a few seconds
                var files = CreateRandomFilesForZip(1000);
                var response = await DeployZip(appManager, files, doAsync: true);
                response.EnsureSuccessStatusCode();

                // Immediately try another deployment
                var response2 = await DeployZip(appManager, new FileForZip[0], doAsync: true);

                Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal("Zip-Push", result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());
            });
        }

        [Fact]
        public Task EnsureConflictResultOnSimultaneousSyncRequest()
        {
            return ApplicationManager.RunAsync("EnsureConflictResultOnSimultaneousSyncRequest", async appManager =>
            {
                // Big enough to run for a few seconds
                var files = CreateRandomFilesForZip(1000);
                var response = await DeployZip(appManager, files, doAsync: true);
                response.EnsureSuccessStatusCode();

                // Immediately try another deployment
                var response2 = await DeployZip(appManager, new FileForZip[0], doAsync: false);

                Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

                DeployResult result;
                do
                {
                    result = await appManager.DeploymentManager.GetResultAsync("latest");
                    Assert.Equal("Zip-Push", result.Deployer);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                } while (!new[] { DeployStatus.Failed, DeployStatus.Success }.Contains(result.Status));

                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());
            });
        }

        [Fact]
        public Task EnsureExpectedKudusyncBehavior()
        {
            return ApplicationManager.RunAsync("EnsureExpectedKudusyncBehavior", async appManager =>
            {
                var origTime = DateTime.Now;
                var files = CreateRandomFilesForZip(10);
                foreach (var file in files)
                {
                    file.ModifiedTime = origTime;
                }

                var response = await DeployZip(appManager, files, doAsync: false);
                response.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByContent(appManager, files);

                // Deploy a new zip with some new files, some modified files, and some old files
                var newFiles = CreateRandomFilesForZip(3);
                var oldfile = files[0];
                var modifiedFile = new FileForZip { Filename = files[1].Filename, Content = "UPDATED", ModifiedTime = DateTime.Now };
                var files2 = newFiles.Concat(new[] { oldfile, modifiedFile }).ToArray();

                var response2 = await DeployZip(appManager, files2, doAsync: false);
                response2.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByContent(appManager, files2);

                // Would need to check the trace log to ensure that the old file wasn't actually copied, but preserved.
            });
        }

        [Fact]
        public Task EnsureProjectSettingHonored()
        {
            return ApplicationManager.RunAsync("EnsureProjectSettingHonored", async appManager =>
            {
                await appManager.SettingsManager.SetValue("PROJECT", "subdir");

                var rootFile = new FileForZip { Filename = "should-not-be-deployed", Content = "" };
                var subdirFile = new FileForZip { Filename = "subdir/should-be-deployed", Content = "" };

                var files = new[] { rootFile, subdirFile };
                var response = await DeployZip(appManager, files, doAsync: false);
                response.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByFilenames(appManager, new[] { "should-be-deployed" });
            });
        }

        [Fact]
        public Task EnsureBuildConfigBehavior()
        {
            return ApplicationManager.RunAsync("EnsureProjectSettingHonored", async appManager =>
            {
                var files = new[]
                {
                    new FileForZip { Filename = "package.json", Content = simplePackageJsonWithDependencyContent}
                };

                // Should do KuduSync copy only, no build (zip deployments are assumed to be complete by default)
                var response = await DeployZip(appManager, files, doAsync: false);
                response.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());

                var files2 = files.Concat(new[]
                {
                    new FileForZip { Filename = ".deployment", Content = "[config]\r\nSCM_DO_BUILD_DURING_DEPLOYMENT = true"}
                }).ToArray();

                await appManager.SettingsManager.SetValue("SCM_DO_BUILD_DURING_DEPLOYMENT", "false");

                // Still won't build, since the site setting should override the .deployment. Note that we don't
                // expect .deployment to be copied to the site root.
                var response2 = await DeployZip(appManager, files2, doAsync: false);
                response2.EnsureSuccessStatusCode();
                await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());

                await appManager.SettingsManager.Delete("SCM_DO_BUILD_DURING_DEPLOYMENT");

                // Deploy once more, now we expect a node build to occur
                var response3 = await DeployZip(appManager, files2, doAsync: false);
                response3.EnsureSuccessStatusCode();

                var expected = files.Concat(new[] { new FileForZip { Filename = "node_modules" } }).ToArray();
                await AssertSuccessfulDeploymentByFilenames(appManager, expected.Select(f => f.Filename).ToArray());
            });
        }

        [Fact]
        public Task TestSideBySideWithGitRepo()
        {
            return ApplicationManager.RunAsync("TestSimpleZipDeployment", async appManager =>
            {
                using (var repo = Git.Init(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())))
                {
                    // First deploy zip
                    var files = CreateRandomFilesForZip(10);
                    var response = await DeployZip(appManager, files, doAsync: false);
                    response.EnsureSuccessStatusCode();
                    await AssertSuccessfulDeploymentByFilenames(appManager, files.Select(f => f.Filename).ToArray());

                    await appManager.SettingsManager.SetValue(SettingsKeys.ScmType, "LocalGit");

                    // Push-deploy git repo
                    var gitFilePath = Path.Combine(repo.PhysicalPath, Path.GetRandomFileName());
                    File.WriteAllText(gitFilePath, Guid.NewGuid().ToString("N"));
                    Git.Commit(repo.PhysicalPath, "Initial commit");
                    var gitResult = appManager.GitDeploy(repo.PhysicalPath);
                    await AssertSuccessfulDeploymentByFilenames(appManager, Directory.GetFiles(repo.PhysicalPath).Select(f => Path.GetFileName(f)).ToArray());

                    // Deploy new zip
                    var files2 = CreateRandomFilesForZip(10);
                    var response2 = await DeployZip(appManager, files2, doAsync: false);
                    response2.EnsureSuccessStatusCode();
                    await AssertSuccessfulDeploymentByFilenames(appManager, files2.Select(f => f.Filename).ToArray());

                    // Redeploy git deployment
                    var id = Git.Id(repo.PhysicalPath);
                    var response3 = await appManager.DeploymentManager.DeployAsync(id);
                    response3.EnsureSuccessStatusCode();
                    await AssertSuccessfulDeploymentByFilenames(appManager, Directory.GetFiles(repo.PhysicalPath).Select(f => Path.GetFileName(f)).ToArray());
                }
            });
        }

        private static async Task AssertSuccessfulDeploymentByContent(ApplicationManager appManager, FileForZip[] files)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);
            Assert.Equal("Zip-Push", deployment.Deployer);

            var entries = await appManager.VfsWebRootManager.ListAsync(null);
            var deployedFilenames = entries.Select(e => e.Name);

            var filenameSet = new HashSet<string>(files.Select(f => f.Filename));
            Assert.True(filenameSet.SetEquals(entries.Select(e => e.Name)));

            foreach (var file in files)
            {
                var deployedContent = await appManager.VfsWebRootManager.ReadAllTextAsync(file.Filename);
                Assert.Equal(file.Content, deployedContent);
            }
        }

        private static async Task AssertSuccessfulDeploymentByFilenames(ApplicationManager appManager, string[] filenames)
        {
            TestTracer.Trace("Verifying files are deployed and deployment record created.");

            var deployment = await appManager.DeploymentManager.GetResultAsync("latest");

            Assert.Equal(DeployStatus.Success, deployment.Status);

            var entries = await appManager.VfsWebRootManager.ListAsync(null);
            var deployedFilenames = entries.Select(e => e.Name);

            var filenameSet = new HashSet<string>(filenames);
            Assert.True(filenameSet.SetEquals(entries.Select(e => e.Name)));
        }

        private static async Task<HttpResponseMessage> DeployZip(ApplicationManager appManager, FileForZip[] files, bool doAsync)
        {
            using (var zipStream = CreateZipStream(files))
            {
                return await DeployZip(appManager, zipStream, doAsync);
            }
        }

        private static async Task<HttpResponseMessage> DeployZip(ApplicationManager appManager, Stream zipStream, bool doAsync)
        {
            TestTracer.Trace("Push-deploying zip");
            var response = await appManager.PushDeploymentManager.PushDeployFromStream(zipStream, doAsync);
            return response;
        }

        private static FileForZip[] CreateRandomFilesForZip(int numFiles)
        {
            return Enumerable.Range(0, numFiles)
                .Select(i => Guid.NewGuid().ToString("N"))
                .Select(s => new FileForZip { Content = s, Filename = s })
                .ToArray();
        }
        private static Stream CreateZipStream(int numFiles, out HashSet<string> fileNames)
        {
            var files = CreateRandomFilesForZip(numFiles);
            var stream = CreateZipStream(files);
            fileNames = new HashSet<string>(files.Select(f => f.Filename));
            return stream;
        }

        private static Stream CreateZipStream(FileForZip[] files)
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

        private static string simplePackageJsonWithDependencyContent = @"{
  ""name"": ""test"",
  ""version"": ""1.0.0"",
  ""dependencies"": {
    ""express"": """"
  }
}
";

        private class FileForZip
        {
            public string Filename;
            public string Content;
            public DateTime? ModifiedTime;
        }
    }
}
