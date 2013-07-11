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
        public async Task InPlaceDeploymentBasicTests(IScenario scenario, Setting setting)
        {
            var appName = KuduUtils.GetRandomWebsiteName(scenario.Name);
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                KeyValuePair<string, string>[] settings = setting.GetSettings();
                if (settings.Length > 0)
                {
                    await appManager.SettingsManager.SetValues(settings);
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
                //yield return new object[] { new SomeNodeJs(), new RepositoryDefaultProjectTargetPathSetting() };

                var scenario = new HelloKuduWithSubFolders();
                foreach (Setting setting in Settings)
                {
                    yield return new object[] { scenario, setting };
                }
            }
        }

        public static IEnumerable<Setting> Settings
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

        public abstract class Setting
        {
            public abstract string RepositoryPath { get; }
            public abstract string Project { get; }
            public abstract string TargetPath { get; }
            public abstract void Verify(ApplicationManager appManager);
            public KeyValuePair<string, string>[] GetSettings()
            {
                var settings = new List<KeyValuePair<string, string>>();

                if (RepositoryPath != "repository")
                {
                    settings.Add(new KeyValuePair<string, string>(SettingsKeys.RepositoryPath, RepositoryPath));
                }

                if (Project != ".")
                {
                    settings.Add(new KeyValuePair<string, string>(SettingsKeys.Project, Project));
                }

                if (TargetPath != ".")
                {
                    settings.Add(new KeyValuePair<string, string>(SettingsKeys.TargetPath, TargetPath));
                }

                return settings.ToArray();
            }
        }

        public interface IScenario
        {
            string Name { get; }
            string CloneUrl { get; }
            string BuilderTrace { get; }

            void Verify(ApplicationManager appManager, Setting setting);
            void GitVerify(GitDeploymentResult result, Setting setting);
            void GitVerifyDoesNotContainKuduSync(GitDeploymentResult result);
        }

        public class InPlaceDefaultProjectTargetPathSetting : Setting
        {
            public string Name
            {
                get
                {
                    return "Scenario : InPlaceDefaultProjectTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = wwwroot, PROJECT = ., SCM_TARGET_PATH = .";
                }
            }

            public override string RepositoryPath
            {
                get { return "wwwroot"; }
            }

            public override string Project
            {
                get { return "."; }
            }

            public override string TargetPath
            {
                get { return "."; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
            }
        }

        public class InPlaceSubFolderProjectSubFolderTargetPathSetting : Setting
        {
            public string Name
            {
                get
                {
                    return "Scenario : InPlaceDefaultProjectTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = wwwroot, PROJECT = subfolder1, SCM_TARGET_PATH = subfolder3";
                }
            }

            public override string RepositoryPath
            {
                get { return "wwwroot"; }
            }

            public override string Project
            {
                get { return "subfolder1"; }
            }

            public override string TargetPath
            {
                get { return "subfolder3"; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder3"), @"Should have site\repository\subfolder3 folder");
            }
        }

        public class RepositoryDefaultProjectTargetPathSetting : Setting
        {
            public string Name
            {
                get
                {
                    return "Scenario : RepositoryDefaultProjectTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = ., SCM_TARGET_PATH = .";
                }
            }

            public override string RepositoryPath
            {
                get { return "repository"; }
            }

            public override string Project
            {
                get { return "."; }
            }

            public override string TargetPath
            {
                get { return "."; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
            }
        }

        public class RepositoryDefaultProjectSubFolderTargetPathSetting : Setting
        {
            public string Name
            {
                get
                {
                    return "Scenario: RepositoryDefaultProjectSubFolderTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = ., SCM_TARGET_PATH = subfolder";
                }
            }

            public override string RepositoryPath
            {
                get { return "repository"; }
            }

            public override string Project
            {
                get { return "."; }
            }

            public override string TargetPath
            {
                get { return "subfolder"; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder"), @"Should have site\repository\subfolder folder");
            }
        }

        public class RepositorySubfolderProjectDefaultTargetPathSetting : Setting
        {
            public string Name
            {
                get
                {
                    return "Scenario: RepositorySubfolderProjectDefaultTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = subfolder1, SCM_TARGET_PATH = .";
                }
            }

            public override string RepositoryPath
            {
                get { return "repository"; }
            }

            public override string Project
            {
                get { return "subfolder1"; }
            }

            public override string TargetPath
            {
                get { return "."; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.False(appManager.VfsManager.Exists(@"site\wwwroot\subfolder2"), @"Should not have site\wwwroot\subfolder2 folder");
            }
        }

        public class RepositorySubfolderProjectSubfolderTargetPathSetting : Setting
        {
            public string Name
            {
                get
                {
                    return "Scenario: RepositorySubfolderProjectDefaultTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = repository, PROJECT = subfolder1, SCM_TARGET_PATH = subfolder3";
                }
            }

            public override string RepositoryPath
            {
                get { return "repository"; }
            }

            public override string Project
            {
                get { return "subfolder1"; }
            }

            public override string TargetPath
            {
                get { return "subfolder3"; }
            }

            public override void Verify(ApplicationManager appManager)
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

            public void Verify(ApplicationManager appManager, Setting setting)
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

            public void GitVerify(GitDeploymentResult result, Setting setting)
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