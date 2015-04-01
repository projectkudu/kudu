using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class CustomGitTests
    {
        [Fact]
        public async Task CustomGitTest()
        {
            await ApplicationManager.RunAsync("TestCustomGit", async appManager =>
            {
                const string fooContent = "This is foo";
                const string barContent = "This is bar";
                // Create some files under wwwroot/foo
                appManager.VfsWebRootManager.WriteAllText("sub/foo.txt", fooContent);
                appManager.VfsWebRootManager.WriteAllText("sub/sub2/bar.txt", barContent);

                // Create a git repo over them
                CommandResult result = await appManager.CommandExecutor.ExecuteCommand(
                    @"git init & git config user.email ""kudu@test.org"" & git config user.name ""kudu"" & git add -A & git commit -m""init""", @"site/wwwroot/sub");
                if (!String.IsNullOrEmpty(result.Error))
                {
                    TestTracer.Trace("Command exec failed: {0}", result.Error);
                    Assert.Equal(0, result.ExitCode);
                }

                // Clone it using the custom git controller
                string repositoryPath = Git.CloneToLocal(appManager.GetCustomGitUrl(@"site/wwwroot/sub"));

                // Verify that the files are there
                string foo = File.ReadAllText(Path.Combine(repositoryPath, "foo.txt"));
                Assert.Equal(fooContent, foo);
                string bar = File.ReadAllText(Path.Combine(repositoryPath, "sub2", "bar.txt"));
                Assert.Equal(barContent, bar);
            });
        }
    }
}
