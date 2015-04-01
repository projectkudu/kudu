using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class InitialCloneTest
    {
        [Theory]
        [MemberData("Scenarios")]
        public async Task InitialCloneBasicTests(IScenario scenario)
        {
            // this contains zip files for each scenario
            using (var repo = Git.Clone("WebDeploySamples"))
            {
                string zipFile = Path.Combine(repo.PhysicalPath, scenario.Name + ".zip");
                Assert.True(File.Exists(zipFile), "Could not find " + zipFile);

                await ApplicationManager.RunAsync(scenario.Name, async appManager =>
                {
                    // moodle takes 5 mins to zip deploy (< 100s default timeout).
                    // appManager.ZipManager.Client.Timeout = TimeSpan.FromMinutes(10);

                    // deploy zip file using /zip
                    appManager.ZipManager.PutZipFile("site/wwwroot", zipFile);

                    // sanity validate content
                    scenario.DeployVerify(appManager.SiteUrl);

                    // simulate LocalGit env
                    // this is actually done at create site.
                    // however, the site reuse cleared this up.
                    await appManager.SettingsManager.SetValue(SettingsKeys.ScmType, "LocalGit");

                    // initial clone to temp folder
                    // this will clone from wwwroot
                    string repositoryPath = Git.CloneToLocal(appManager.GitUrl);

                    try
                    {
                        // we should have set the SCM_REPOSITORY_PATH to wwwroot
                        var value = await appManager.SettingsManager.GetValue(SettingsKeys.RepositoryPath);
                        Assert.Equal("wwwroot", value);

                        // make local modification
                        scenario.ApplyChange(repositoryPath);

                        // commit and push
                        Git.Commit(repositoryPath, "new change");
                        Git.Push(repositoryPath, appManager.GitUrl);

                        // validate deployment status
                        var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                        Assert.Equal(1, results.Count);
                        Assert.Equal(DeployStatus.Success, results[0].Status);

                        // validate changed content
                        scenario.ChangeVerify(appManager.SiteUrl);
                    }
                    finally
                    {
                        SafeExecute(() => Directory.Delete(repositoryPath, recursive: true));
                    }
                });
            }
        }

        public static IEnumerable<object[]> Scenarios
        {
            get
            {
                yield return new object[] { new NodeJsAppScenario() };
                yield return new object[] { new BasicWebSiteAppScenario() };
                yield return new object[] { new MvcApplicationScenario() };

                // only run in private kudu
                //yield return new object[] { new HelloKuduScenario() };
                //yield return new object[] { new BakeryAppScenario() };

                // big site
                //yield return new object[] { new MoodleAppScenario() };
            }
        }

        private void SafeExecute(Action action)
        {
            try
            {
                action();
            }
            catch
            {
            }
        }

        public interface IScenario
        {
            string Name { get; }
            void DeployVerify(string siteUrl);
            void ApplyChange(string path);
            void ChangeVerify(string siteUrl);
        }

        public class HelloKuduScenario : IScenario
        {
            public string Name
            {
                get { return "HelloKudu"; }
            }

            public void DeployVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl + "index.html", "Hello Kudu");
            }

            public void ApplyChange(string path)
            {
                string indexHtml = Path.Combine(path, "index.html");
                Assert.True(File.Exists(indexHtml));
                File.WriteAllText(indexHtml, "<h1>Hello New Kudu</h1>");
            }

            public void ChangeVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl + "index.html", "Hello New Kudu");
            }
        }

        public class NodeJsAppScenario : IScenario
        {
            public string Name
            {
                get { return "NodeJsApp"; }
            }

            public void DeployVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Hello, world");
            }

            public void ApplyChange(string path)
            {
                string serverJs = Path.Combine(path, "server.js");
                Assert.True(File.Exists(serverJs));
                string text = File.ReadAllText(serverJs);
                File.WriteAllText(serverJs, text.Replace("Hello, world", "Hello, new world"));
            }

            public void ChangeVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Hello, new world");
            }
        }

        public class BasicWebSiteAppScenario : IScenario
        {
            public string Name
            {
                get { return "BasicWebSiteApp"; }
            }

            public void DeployVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "To learn more about");
            }

            public void ApplyChange(string path)
            {
                string defaultCSHtml = Path.Combine(path, "Default.cshtml");
                Assert.True(File.Exists(defaultCSHtml));
                string text = File.ReadAllText(defaultCSHtml);
                File.WriteAllText(defaultCSHtml, text.Replace("To learn more about", "To learn even more about"));
            }

            public void ChangeVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "To learn even more about");
            }
        }

        public class MvcApplicationScenario : IScenario
        {
            public string Name
            {
                get { return "MvcApplication"; }
            }

            public void DeployVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Modify this template to jump-start");
            }

            public void ApplyChange(string path)
            {
                string controllerCS = Path.Combine(path, @"Controllers\HomeController.cs");
                Assert.True(File.Exists(controllerCS));
                string text = File.ReadAllText(controllerCS);
                File.WriteAllText(controllerCS, text.Replace("Modify this template to jump-start", "Modify this new template to jump-start"));
            }

            public void ChangeVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Modify this new template to jump-start");
            }
        }

        public class BakeryAppScenario : IScenario
        {
            public string Name
            {
                get { return "BakeryApp"; }
            }

            public void DeployVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Welcome to Fourth Coffee");
            }

            public void ApplyChange(string path)
            {
                string defaultCSHtml = Path.Combine(path, "Default.cshtml");
                Assert.True(File.Exists(defaultCSHtml));
                string text = File.ReadAllText(defaultCSHtml);
                File.WriteAllText(defaultCSHtml, text.Replace("Welcome to Fourth Coffee", "Welcome to Fifth Coffee"));
            }

            public void ChangeVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Welcome to Fifth Coffee");
            }
        }

        public class MoodleAppScenario : IScenario
        {
            public string Name
            {
                get { return "MoodleApp"; }
            }

            public void DeployVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl + "install.php", "Choose a language");
            }

            public void ApplyChange(string path)
            {
                string installPHP = Path.Combine(path, @"install\lang\en\install.php");
                Assert.True(File.Exists(installPHP));
                string text = File.ReadAllText(installPHP);
                File.WriteAllText(installPHP, text.Replace("Choose a language", "Choose a NEW language"));
            }

            public void ChangeVerify(string siteUrl)
            {
                KuduAssert.VerifyUrl(siteUrl, "Choose a NEW language");
            }
        }
    }
}