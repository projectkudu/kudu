using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    public class InPlaceDeploymentTest
    {
        [Theory]
        [PropertyData("ScenarioSettings")]
        public async Task InPlaceDeploymentBasicTests(IScenario scenario, ISetting setting)
        {
            var appName = KuduUtils.GetRandomWebsiteName(scenario.Name);
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                if (setting.RepositoryPath != "repository")
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.RepositoryPath, setting.RepositoryPath);
                }

                if (setting.Project != ".")
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.Project, setting.Project);
                }

                if (setting.TargetPath != ".")
                {
                    await appManager.SettingsManager.SetValue(SettingsKeys.TargetPath, setting.TargetPath);
                }

                using (TestRepository testRepository = Git.Clone(scenario.Name, scenario.CloneUrl))
                {
                    var result = appManager.GitDeploy(testRepository.PhysicalPath);                    
                    setting.Verify(appManager);
                    scenario.GitVerify(result, setting);
                }

                // Validate deployment status
                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);

                // Validate site
                scenario.Verify(appManager, setting);                
            });
        }

        public static IEnumerable<object[]> ScenarioSettings
        {
            get
            {
                foreach (object scenario in Scenarios)
                {
                    foreach (object setting in Settings)
                    {                            
                        yield return new object[] {(IScenario)scenario, (ISetting)setting};
                    }
                }
            }
        }

        public static IEnumerable<ISetting> Settings
        {
            get
            {
                yield return new InPlaceDefaultProjectTargetPathSetting();
                yield return new InPlaceSubFolderProjectSubFolderTargetPathSetting();
                yield return new RepositoryDefaultProjectTargetPathSetting();
                yield return new RepositoryDefaultProjectSubFolderTargetPathSetting();
                yield return new RepositorySubfolderProjectDefaultTargetPathSetting();
                yield return new RepositorySubfolderProjectSubfolderTargetPathSetting();
            }
        }

        public static IEnumerable<IScenario> Scenarios
        {
            get
            {
                yield return new HelloKuduWithSubFolders();
            }
        }

        public interface ISetting
        {
            string RepositoryPath { get; }
            string Project { get; }
            string TargetPath { get; }
            void Verify(ApplicationManager appManager);
        }

        public interface IScenario
        {
            string Name { get; }
            string CloneUrl { get; }
            string BuilderTrace { get; }

            void Verify(ApplicationManager appManager, ISetting setting);            
            void GitVerify(GitDeploymentResult result, ISetting setting);
            void GitVerifyDoesNotContainKuduSync(GitDeploymentResult result);
        }

        public class InPlaceDefaultProjectTargetPathSetting : ISetting
        {
            public string Name
            {
                get { return "Scenario : InPlaceDefaultProjectTargetPathSetting, "
                + "Setting : SCM_REPOSITORY_PATH = wwwroot, PROJECT = ., SCM_TARGET_PATH = ."; }
            }

            public string RepositoryPath
            {
                get { return "wwwroot"; }
            }

            public string Project
            {
                get { return "."; }
            }

            public string TargetPath
            {
                get { return "."; }
            }

            public void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
            }
        }

        public class InPlaceSubFolderProjectSubFolderTargetPathSetting : ISetting
        {
            public string Name
            {
                get { return "Scenario : InPlaceDefaultProjectTargetPathSetting, "
                + "Setting : SCM_REPOSITORY_PATH = wwwroot, PROJECT = subfolder1, SCM_TARGET_PATH = subfolder3"; }
            }

            public string RepositoryPath
            {
                get { return "wwwroot"; }
            }

            public string Project
            {
                get { return "subfolder1"; }
            }

            public string TargetPath
            {
                get { return "subfolder3"; }
            }

            public void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder3"), @"Should have site\repository\subfolder3 folder");
            }
        }

        public class RepositoryDefaultProjectTargetPathSetting : ISetting
        {
            public string Name
            {
                get { return "Scenario : RepositoryDefaultProjectTargetPathSetting, "
                + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = ., SCM_TARGET_PATH = ."; }
            }

            public string RepositoryPath
            {
                get { return "repository"; }
            }

            public string Project
            {
                get { return "."; }
            }

            public string TargetPath
            {
                get { return "."; }
            }

            public void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
            }
        }

        public class RepositoryDefaultProjectSubFolderTargetPathSetting : ISetting
        {
            public string Name
            {
                get { return "Scenario: RepositoryDefaultProjectSubFolderTargetPathSetting, "
                + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = ., SCM_TARGET_PATH = subfolder"; }
            }

            public string RepositoryPath
            {
                get { return "repository"; }
            }

            public string Project
            {
                get { return "."; }
            }

            public string TargetPath
            {
                get { return "subfolder"; }
            }

            public void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder"), @"Should have site\repository\subfolder folder");
            }
        }

        public class RepositorySubfolderProjectDefaultTargetPathSetting : ISetting
        {
            public string Name
            {
                get { return "Scenario: RepositorySubfolderProjectDefaultTargetPathSetting, "
                + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = subfolder1, SCM_TARGET_PATH = ."; }
            }

            public string RepositoryPath
            {
                get { return "repository"; }
            }

            public string Project
            {
                get { return "subfolder1"; }
            }

            public string TargetPath
            {
                get { return "."; }
            }

            public void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.False(appManager.VfsManager.Exists(@"site\wwwroot\subfolder2"), @"Should not have site\wwwroot\subfolder2 folder");
            }
        }

        public class RepositorySubfolderProjectSubfolderTargetPathSetting : ISetting
        {
            public string Name
            {
                get { return "Scenario: RepositorySubfolderProjectDefaultTargetPathSetting, "
                + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = subfolder1, SCM_TARGET_PATH = subfolder3"; }
            }

            public string RepositoryPath
            {
                get { return "repository"; }
            }

            public string Project
            {
                get { return "subfolder1"; }
            }

            public string TargetPath
            {
                get { return "subfolder3"; }
            }

            public void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder3"), @"Should have site\wwwroot\subfolder3 folder");
            }
        }

        public class HelloKuduWithSubFolders : IScenario
        {
            public string Name
            {
                get { return "HelloKuduWithSubFolders"; }
            }

            public string CloneUrl
            {
                get { return "https://github.com/KuduApps/HelloKuduWithSubFolders.git"; }
            }

            public string BuilderTrace
            {
                get { return "Handling Basic Web Site deployment"; }
            }

            public void Verify(ApplicationManager appManager, ISetting setting)
            {
                if (setting.TargetPath == ".")
                {
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "index.htm", "Hello Kudu");
                }
                else
                {
                    Verify(appManager, setting.TargetPath);
                }
            }

            private void Verify(ApplicationManager appManager, string documentRoot)
            {
                KuduAssert.VerifyUrl(appManager.SiteUrl + documentRoot + @"/index.htm", "Hello Kudu");
            }

            public void GitVerify(GitDeploymentResult result, ISetting setting)
            {
                Assert.Contains(BuilderTrace, result.GitTrace);

                if (setting.RepositoryPath == "wwwroot" && setting.TargetPath == ".")
                {
                    GitVerifyDoesNotContainKuduSync(result);
                }
            }

            public void GitVerifyDoesNotContainKuduSync(GitDeploymentResult result)
            {
                Assert.DoesNotContain("KuduSync", result.GitTrace);
            }
        }
    }
}