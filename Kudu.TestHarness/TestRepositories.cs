using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.TestHarness
{
    class TestRepositories
    {
        private static TestRepositoryInfo[] _repoList = new TestRepositoryInfo[]
        {
            new TestRepositoryInfo("https://github.com/KuduApps/HelloWorld.git",                    "1f3dfd8"),
            new TestRepositoryInfo("https://github.com/KuduApps/HelloKudu.git",                     "2370e44"),
            new TestRepositoryInfo("https://github.com/KuduApps/VersionPinnedNodeJsApp.git",        "9461da6"),
            new TestRepositoryInfo("https://github.com/KuduApps/CustomDeploymentSettingsTest.git",  "66e15f2"),
            new TestRepositoryInfo("https://github.com/KuduApps/Mvc3AppWithTestProject.git",        "5df86e3"),
            new TestRepositoryInfo("https://github.com/KuduApps/CustomBuildScript.git",             "2c156bd"),
            new TestRepositoryInfo("https://github.com/KuduApps/WarningsAsErrors.git",              "51b6a48"),
            new TestRepositoryInfo("https://github.com/KuduApps/Mvc3Application_NoSolution.git",    "4e36ca3"),
            new TestRepositoryInfo("https://github.com/KuduApps/NoDeployableProjects.git",          "e21eb82"),
            new TestRepositoryInfo("https://github.com/KuduApps/ConditionalCompilation.git",        "a104578"),
            new TestRepositoryInfo("https://github.com/KuduApps/waws.git",                          "7652a66"),
            new TestRepositoryInfo("https://github.com/KuduApps/Bakery.git",                        "2f29dc6"),
            new TestRepositoryInfo("https://github.com/KuduApps/NpmSite.git",                       "2b29b9d"),
            new TestRepositoryInfo("https://github.com/KuduApps/NodeHelloWorldNoConfig.git",        "d8a15b6"),
            new TestRepositoryInfo("git@github.com:KuduQAOrg/RepoWithPrivateSubModule.git",         "4820516"),
            new TestRepositoryInfo("https://github.com/KuduApps/HangProcess.git",                   "2e53ab6"),
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
