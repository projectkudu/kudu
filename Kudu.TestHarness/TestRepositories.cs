using System;
using System.Collections.Generic;
using System.IO;

namespace Kudu.TestHarness
{
    internal class TestRepositories
    {
        // Make sure these are kept sorted alphabetically to reduce merge conflicts
        private static TestRepositoryInfo[] _repoList = new TestRepositoryInfo[]
        {
            new TestRepositoryInfo("git@github.com:KuduQAOrg/RepoWithPrivateSubModule.git",         "4820516"),
            new TestRepositoryInfo("https://github.com/KuduApps/-benr-.git",                        "c553978"),
            new TestRepositoryInfo("https://github.com/KuduApps/AppWithPostBuildEvent.git",         "083b651"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore1.0.0VS17WithLib.git",    "4143130"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore1.1.0VS17WithLib.git",    "3465591"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore2.0CliWithLib.git",       "afdb55e"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore2.0.0Cli.git",            "27ab60f"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore2.0.0VS17WithLib.git",    "23d74c5"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore2.1.0VS17WithLib.git",    "c8bb987"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCore2.1.0WebApiCli.git",      "e182a89"),
            new TestRepositoryInfo("https://github.com/KuduApps/AspNetCoreRC4WebApiVsSln.git",      "61585d7"),
            new TestRepositoryInfo("https://github.com/KuduApps/Bakery.git",                        "2f29dc6"),
            new TestRepositoryInfo("https://github.com/KuduApps/BasicConsoleWorker.git",            "ce1ec74"),
            new TestRepositoryInfo("https://github.com/KuduApps/ChakraMsieTest.git",                "c162f39"),
            new TestRepositoryInfo("https://github.com/KuduApps/ConditionalCompilation.git",        "946159e"),
            new TestRepositoryInfo("https://github.com/KuduApps/ConsoleWorker.git",                 "a2c2bc7"),
            new TestRepositoryInfo("https://github.com/KuduApps/CustomBuildScript.git",             "2c156bd"),
            new TestRepositoryInfo("https://github.com/KuduApps/CustomDeploymentSettingsTest.git",  "bc0ce04"),
            new TestRepositoryInfo("https://github.com/KuduApps/CSharp6Web.git",                    "41d748f"),
            new TestRepositoryInfo("https://github.com/KuduApps/Dev11_Net45_Mvc4_WebAPI.git",       "bb9e6b4"),
            new TestRepositoryInfo("https://github.com/KuduApps/DnxWebJobTest.git",                 "8450950"),
            new TestRepositoryInfo("https://github.com/KuduApps/Express-Template.git",              "0878ac7"),
            new TestRepositoryInfo("https://github.com/KuduApps/fsharp-owin-sample.git",            "c29e8ae"),
            new TestRepositoryInfo("https://github.com/KuduApps/HangProcess.git",                   "e1c6c52"),
            new TestRepositoryInfo("https://github.com/KuduApps/HelloKudu.git",                     "2370e44"),
            new TestRepositoryInfo("https://github.com/KuduApps/HelloKuduWithSubFolders.git",       "617fe07"),
            new TestRepositoryInfo("https://github.com/KuduApps/HelloWorld.git",                    "1f3dfd8"),
            new TestRepositoryInfo("https://github.com/KuduApps/HiddenFoldersAndFiles.git",         "7bf5e6d"),
            new TestRepositoryInfo("https://github.com/KuduApps/Html5Test.git",                     "7262300"),
            new TestRepositoryInfo("https://github.com/KuduApps/kuduglob.git",                      "eff5c35"),
            new TestRepositoryInfo("https://github.com/KuduApps/LogTester.git",                     "f20ab64"),
            new TestRepositoryInfo("https://github.com/KuduApps/Mvc3Application.git",               "276823b"),
            new TestRepositoryInfo("https://github.com/KuduApps/Mvc3Application_NoSolution.git",    "6c2f65d"),
            new TestRepositoryInfo("https://github.com/KuduApps/Mvc3AppWithTestProject.git",        "b8c4213"),
            new TestRepositoryInfo("https://github.com/KuduApps/MvcApplicationEFSqlCompact.git",    "c2fa9c0"),
            new TestRepositoryInfo("https://github.com/KuduApps/MvcApplicationWithNuGetAutoRestore","1e269b5"),
            new TestRepositoryInfo("https://github.com/KuduApps/MVCAppWithLatestNuget.git",         "7f49749"),
            new TestRepositoryInfo("https://github.com/KuduApps/MvcAppWithTypeScript.git",          "7319dbb"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeHelloWorldNoConfig.git",        "d8a15b6"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeInnerSubDir.git",               "58f93f8"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeJsVS17project.git",             "1fb0bd9"),
            new TestRepositoryInfo("https://github.com/KuduApps/NoDeployableProjects.git",          "04e9920"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeWebHookTest.git",               "cd5bee7"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeWithCustomStartScript.git",     "a142e44"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeWithSolutions.git",             "bd16404"),
            new TestRepositoryInfo("https://github.com/KuduApps/NpmSite.git",                       "2b29b9d"),
            new TestRepositoryInfo("https://github.com/KuduApps/Orchard.git",                       "09ce5a5"),
            new TestRepositoryInfo("https://github.com/KuduApps/PreviewMvc5.git",                   "f361ad3"),
            new TestRepositoryInfo("https://github.com/KuduApps/PreviewSpa5.git",                   "9f6cde2"),
            new TestRepositoryInfo("https://github.com/KuduApps/PreviewWebApi5.git",                "74a0ce0"),
            new TestRepositoryInfo("https://github.com/KuduApps/ProjectKWebApplication.git",        "fa5f2bc"),
            new TestRepositoryInfo("https://github.com/KuduApps/ProjectWithNoSolution.git",         "5460398"),
            new TestRepositoryInfo("https://github.com/KuduApps/SpecificDeploymentConfiguration.git","fa99bb8"),
            new TestRepositoryInfo("https://github.com/KuduApps/TargetPathTest.git",                "7446104"),
            new TestRepositoryInfo("https://github.com/KuduApps/VersionPinnedNodeJsApp.git",        "5f6c64f"),
            new TestRepositoryInfo("https://github.com/KuduApps/VersionPinnedNodeJsAppCustom.git",  "8d43d4e"),
            new TestRepositoryInfo("https://github.com/KuduApps/WaitForUserInput.git",              "cb35610"),
            new TestRepositoryInfo("https://github.com/KuduApps/WarningsAsErrors.git",              "51b6a48"),
            new TestRepositoryInfo("https://github.com/KuduApps/waws.git",                          "7652a66"),
            new TestRepositoryInfo("https://github.com/KuduApps/WebDeploySamples.git",              "ee3a42b"),
            new TestRepositoryInfo("https://github.com/KuduApps/WebSiteInSolution.git",             "efee8af"),
            // Make sure these are kept sorted alphabetically to reduce merge conflicts. Don't just add at the end!
        };

        private static Dictionary<string, TestRepositoryInfo> _repos;

        public static TestRepositoryInfo Get(string name)
        {
            if (_repos == null)
            {
                _repos = PopulateRepoDictionary();
            }

            return _repos[name];
        }

        private static Dictionary<string, TestRepositoryInfo> PopulateRepoDictionary()
        {
            var repos = new Dictionary<string, TestRepositoryInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var repo in _repoList)
            {
                string repoName = Path.GetFileNameWithoutExtension(repo.Url);
                repos[repoName] = repo;
            }

            return repos;
        }
    }

    public class TestRepositoryInfo
    {
        public TestRepositoryInfo(string url, string commitId)
        {
            Url = url;
            CommitId = commitId;
        }

        public string Url { get; set; }

        public string CommitId { get; set; }
    }
}
