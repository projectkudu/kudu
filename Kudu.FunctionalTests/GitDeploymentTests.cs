using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Contracts.Settings;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class AspNetAppOrchardTests : GitDeploymentTests
    {
        // ASP.NET apps

        [Fact]
        public void PushAndDeployAspNetAppOrchard()
        {
            PushAndDeployApps("Orchard", "master", "Welcome to Orchard", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppProjectWithNoSolutionTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNetAppProjectWithNoSolution()
        {
            PushAndDeployApps("ProjectWithNoSolution", "master", "Project without solution", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppHiddenFoldersAndFilesTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNetAppHiddenFoldersAndFiles()
        {
            PushAndDeployApps("HiddenFoldersAndFiles", "master", "Hello World", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class WebApiAppTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployWebApiApp()
        {
            PushAndDeployApps("Dev11_Net45_Mvc4_WebAPI", "master", "HelloWorld", HttpStatusCode.OK, "", resourcePath: "api/values", httpMethod: "POST", jsonPayload: "\"HelloWorld\"");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppWebSiteInSolutionTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNetAppWebSiteInSolution()
        {
            PushAndDeployApps("WebSiteInSolution", "master", "SomeDummyLibrary.Class1", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppWebSiteInSolutionWithDeploymentFileTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNetAppWebSiteInSolutionWithDeploymentFile()
        {
            PushAndDeployApps("WebSiteInSolution", "UseDeploymentFile", "SomeDummyLibrary.Class1", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppKuduGlobTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNetAppKuduGlob()
        {
            PushAndDeployApps("kuduglob", "master", "ASP.NET MVC", HttpStatusCode.OK, "酷度");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppUnicodeNameTests : GitDeploymentTests
    {
        // Still needs more work in the deployment script to work
        // [Fact]
        public void PushAndDeployAspNetAppUnicodeName()
        {
            PushAndDeployApps("-benr-", "master", "Hello!", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class AspNetAppAppWithPostBuildEventTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNetAppAppWithPostBuildEvent()
        {
            PushAndDeployApps("AppWithPostBuildEvent", "master", "Hello Kudu", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class FSharpWebApplicationTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployFSharpWebApplication()
        {
            PushAndDeployApps("fsharp-owin-sample", "master", "Owin Sample with F#", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class NodeAppExpressTests : GitDeploymentTests
    {
        // Node apps

        [Fact]
        public void PushAndDeployNodeAppExpress()
        {
            // Ensure node is installed.
            Assert.Contains("nodejs", System.Environment.GetEnvironmentVariable("Path"), StringComparison.OrdinalIgnoreCase);

            PushAndDeployApps("Express-Template", "master", "Modify this template to jump-start your Node.JS Express Web Pages application", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class Html5WithAppJsTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployHtml5WithAppJs()
        {
            PushAndDeployApps("Html5Test", "master", "html5", HttpStatusCode.OK, String.Empty);
        }
    }

    [KuduXunitTestClass]
    public class EFMVC45AppSqlCompactMAPEIATests : GitDeploymentTests
    {
        //Entity Framework 4.5 MVC Project with SQL Compact DB (.sdf file)
        //and Metadata Artifact Processing set to 'Embed in Assembly'
        [Fact]
        public void PushAndDeployEFMVC45AppSqlCompactMAPEIA()
        {
            PushAndDeployApps("MvcApplicationEFSqlCompact", "master", "Reggae", HttpStatusCode.OK, "");
        }
    }

    [KuduXunitTestClass]
    public class CustomDeploymentScriptShouldHaveDeploymentSettingTests : GitDeploymentTests
    {
        // Other apps

        [Fact]
        public async Task CustomDeploymentScriptShouldHaveDeploymentSetting()
        {
            // use a fresh guid so its impossible to accidently see the right output just by chance.
            var guidtext = Guid.NewGuid().ToString();
            var unicodeText = "酷度酷度";
            var normalVar = "TESTED_VAR";
            var normalVarText = "Settings Were Set Properly" + guidtext;
            var kuduSetVar = "KUDU_SYNC_CMD";
            var kuduSetVarText = "Fake Kudu Sync " + guidtext;
            var expectedLogFeedback = "Using custom deployment setting for {0} custom value is '{1}'.".FormatCurrentCulture(kuduSetVar, kuduSetVarText);

            string randomTestName = "CustomDeploymentScriptShouldHaveDeploymentSetting";
            await ApplicationManager.RunAsync(randomTestName, async appManager =>
            {
                appManager.SettingsManager.SetValue(normalVar, normalVarText).Wait();
                appManager.SettingsManager.SetValue(kuduSetVar, kuduSetVarText).Wait();

                // Act
                using (TestRepository testRepository = Git.Clone("CustomDeploymentSettingsTest"))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, "master");
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                // Assert
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);

                // Also validate custom script output supports unicode
                string[] expectedStrings = {
                    unicodeText,
                    normalVar + "=" + normalVarText,
                    kuduSetVar + "=" + kuduSetVarText,
                    expectedLogFeedback };
                KuduAssert.VerifyLogOutput(appManager, results[0].Id, expectedStrings);

                var ex = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(() => appManager.DeploymentManager.GetDeploymentScriptAsync());
                Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
                Assert.Contains("Operation only supported if not using a custom deployment script", ex.ResponseMessage.ExceptionMessage);
            });
        }
    }

    [KuduXunitTestClass]
    public class HelloKuduWithCorruptedGitTestsTests : GitDeploymentTests
    {
        [Fact]
        public async Task PushHelloKuduWithCorruptedGitTests()
        {
            const string randomTestName = "PushHelloKuduWithCorruptedGitTests";
            await ApplicationManager.RunAsync(randomTestName, async appManager =>
            {
                // Act
                using (TestRepository testRepository = Git.Clone("HelloKudu"))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath);
                    var results = await appManager.DeploymentManager.GetResultsAsync();

                    // Assert
                    Assert.Equal(1, results.Count());
                    Assert.Equal(DeployStatus.Success, results.ElementAt(0).Status);

                    var content = await appManager.VfsManager.ReadAllTextAsync("site/repository/.git/HEAD");
                    Assert.Equal("ref: refs/heads/master", content.Trim());

                    // Corrupt the .git/HEAD file
                    appManager.VfsManager.WriteAllBytes("site/repository/.git/HEAD", new byte[23]);
                    content = await appManager.VfsManager.ReadAllTextAsync("site/repository/.git/HEAD");
                    Assert.Equal('\0', content[0]);

                    testRepository.WriteFile("somefile.txt", String.Empty);
                    Git.Commit(testRepository.PhysicalPath, "some commit");

                    var result = appManager.GitDeploy(testRepository.PhysicalPath);

                    content = await appManager.VfsManager.ReadAllTextAsync("site/repository/.git/HEAD");
                    Assert.Equal("ref: refs/heads/master", content.Trim());

                    results = await appManager.DeploymentManager.GetResultsAsync();

                    // Assert
                    Assert.Equal(2, results.Count());
                    Assert.Equal(DeployStatus.Success, results.ElementAt(0).Status);
                    Assert.Equal(DeployStatus.Success, results.ElementAt(1).Status);
                }
            });
        }
    }

    [KuduXunitTestClass]
    public class CustomGeneratorArgsTests : GitDeploymentTests
    {
        [Fact]
        public async Task CustomGeneratorArgs()
        {
            await ApplicationManager.RunAsync("UpdatedTargetPathShouldChangeDeploymentDestination", async appManager =>
            {
                // Even though it's a WAP, use custom script generator arguments to treat it as a web site,
                // deploying only its content folder
                await appManager.SettingsManager.SetValue(SettingsKeys.ScriptGeneratorArgs, "--basic -p MvcApplication14/content");

                using (TestRepository testRepository = Git.Clone("Mvc3AppWithTestProject"))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, "master");
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl + "themes/base/jquery.ui.accordion.css", ".ui-accordion-header");
            });
        }
    }

    [KuduXunitTestClass]
    public class UpdatedTargetPathShouldChangeDeploymentDestinationTests : GitDeploymentTests
    {
        [Fact]
        public void UpdatedTargetPathShouldChangeDeploymentDestination()
        {
            ApplicationManager.Run("UpdatedTargetPathShouldChangeDeploymentDestination", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("TargetPathTest"))
                {
                    appManager.GitDeploy(testRepository.PhysicalPath, "master");
                }
                var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);
                KuduAssert.VerifyUrl(appManager.SiteUrl + "myTarget/index.html", "Target Path Test");
            });
        }
    }

    [KuduXunitTestClass]
    public class MVCAppWithLatestNugetTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployMVCAppWithLatestNuget()
        {
            PushAndDeployApps("MVCAppWithLatestNuget", "master", "MVCAppWithLatestNuget", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class MVCAppWithNuGetAutoRestoreTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployMVCAppWithNuGetAutoRestore()
        {
            PushAndDeployApps("MvcApplicationWithNuGetAutoRestore", "master", "MvcApplicationWithNuGetAutoRestore", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class MvcAppWithAutoRestoreFailsIfRestoreFailsTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployMvcAppWithAutoRestoreFailsIfRestoreFails()
        {
            PushAndDeployApps("MvcApplicationWithNuGetAutoRestore", "bad-config", null, HttpStatusCode.OK, "Unable to find version '1.2.34' of package 'Package.That.Should.NotExist'.", DeployStatus.Failed);
        }
    }

    [KuduXunitTestClass]
    public class MvcAppWithTypeScriptTests : GitDeploymentTests
    {
        [Fact]
        [KuduXunitTest(PrivateOnly = true)]
        public void PushAndDeployMvcAppWithTypeScript()
        {
            PushAndDeployApps("MvcAppWithTypeScript", "master", "Hello, TypeScript Footer!", HttpStatusCode.OK, "Deployment successful", resourcePath: "/Scripts/ts/FooterUpdater.js");
        }
    }

    [KuduXunitTestClass]
    public class PreviewWebApi5Tests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployPreviewWebApi5()
        {
            PushAndDeployApps("PreviewWebApi5", "master", "ASP.NET Preview WebAPI 5", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class PreviewSpa5Tests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployPreviewSpa5()
        {
            PushAndDeployApps("PreviewSpa5", "master", "Preview SPA 5", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class PreviewMvc5Tests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployPreviewMvc5()
        {
            PushAndDeployApps("PreviewMvc5", "master", "ASP.NET Preview MVC5 App", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class AspNet5WithSlnTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNet5WithSln()
        {
            PushAndDeployApps("AspNet5With2ProjectsAndSlnFile", "master", "Welcome from ClassLibrary", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class AspNet5NoSlnTests : GitDeploymentTests
    {
        [Fact]
        public void PushAndDeployAspNet5NoSln()
        {
            PushAndDeployApps("AspNet5With2ProjectsNoSlnFile", "master", "Welcome from ClassLibrary", HttpStatusCode.OK, "Deployment successful");
        }
    }

    [KuduXunitTestClass]
    public class CSharp6WebTests : GitDeploymentTests
    {
        [Fact]
        [KuduXunitTest(PrivateOnly = true)]
        public void PushAndDeployCSharp6Web()
        {
            PushAndDeployApps("CSharp6Web", "master", "ASP.NET is a free web framework", HttpStatusCode.OK, "Deployment successful");
        }
    }

    public abstract class GitDeploymentTests
    {
        //Common code
        internal static void PushAndDeployApps(string repoCloneUrl, string defaultBranchName, string verificationText, 
                                              HttpStatusCode expectedResponseCode, string verificationLogText, 
                                              DeployStatus expectedStatus = DeployStatus.Success, string resourcePath = "", 
                                              string httpMethod = "GET", string jsonPayload = "", bool deleteSCM = false)
        {
            using (new LatencyLogger("PushAndDeployApps - " + repoCloneUrl))
            {
                Uri uri;
                if (!Uri.TryCreate(repoCloneUrl, UriKind.Absolute, out uri))
                {
                    uri = null;
                }

                string randomTestName = uri != null ? Path.GetFileNameWithoutExtension(repoCloneUrl) : repoCloneUrl;
                ApplicationManager.Run(randomTestName, appManager =>
                {
                    // Act
                    using (TestRepository testRepository = Git.Clone(randomTestName, uri != null ? repoCloneUrl : null))
                    {
                        using (new LatencyLogger("GitDeploy"))
                        {
                            appManager.GitDeploy(testRepository.PhysicalPath, defaultBranchName);
                        }
                    }
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(expectedStatus, results[0].Status);
                    var url = new Uri(new Uri(appManager.SiteUrl), resourcePath);
                    if (!String.IsNullOrEmpty(verificationText))
                    {
                        KuduAssert.VerifyUrl(url.ToString(), verificationText, expectedResponseCode, httpMethod, jsonPayload);
                    }
                    if (!String.IsNullOrEmpty(verificationLogText))
                    {
                        KuduAssert.VerifyLogOutput(appManager, results[0].Id, verificationLogText.Trim());
                    }
                    if (deleteSCM)
                    {
                        using (new LatencyLogger("SCMAndWebDelete"))
                        {
                            appManager.RepositoryManager.Delete(deleteWebRoot: false, ignoreErrors: false).Wait();
                        }
                    }
                });
            }
        }
    }
}
