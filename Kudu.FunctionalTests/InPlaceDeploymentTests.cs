using System.Collections.Generic;
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
    public class InPlaceDeploymentTest
    {
        [Theory]
        [MemberData("ScenarioSettings")]
        public async Task InPlaceDeploymentBasicTests(Scenario scenario, Setting setting)
        {
            var appName = scenario.Name;
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                KeyValuePair<string, string>[] settings = setting.GetSettings();
                if (settings.Length > 0)
                {
                    await appManager.SettingsManager.SetValues(settings);
                }

                using (TestRepository testRepository = Git.Clone(scenario.Name))
                {
                    var result = appManager.GitDeploy(testRepository.PhysicalPath);
                    setting.Verify(appManager);
                    scenario.GitVerify(result, setting);
                }

                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                scenario.ValidateDeploymentStatus(results, setting);

                scenario.VerifyUrl(appManager, setting);
            });
        }

        public static IEnumerable<object[]> ScenarioSettings
        {
            get
            {
                yield return new object[] { new NodeJsAppScenario(), new InPlaceDefaultProjectTargetPathSetting() };

                var scenario = new HelloKuduWithSubFolders();
                foreach (Setting setting in Settings)
                {
                    yield return new object[] { scenario, setting };
                }

                yield return new object[] { new HelloKuduWithAboluteFolder(), new AbsoluteTargetPathSetting() };
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
                yield return new InPlaceErrorCaseSetting();
            }
        }

        public abstract class Setting
        {
            public abstract string Name { get; }
            public abstract string RepositoryPath { get; }
            public abstract string Project { get; }
            public abstract string TargetPath { get; }
            public abstract void Verify(ApplicationManager appManager);
            public abstract DeployStatus DeploymentStatus { get; }
            public virtual IEnumerable<string> ContainStrings { get { return new string[0]; } }
            public virtual IEnumerable<string> NotContainStrings { get { return new string[0]; } }

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


        public class InPlaceErrorCaseSetting : Setting
        {
            public override string Name
            {
                get
                {
                    return "Scenario : InPlaceErrorCaseSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = wwwroot, PROJECT = subfolder1, SCM_TARGET_PATH = subfolder2";
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
                get { return "."; }
            }

            public override IEnumerable<string> ContainStrings
            {
                get
                {
                    return new[] 
                    {
                        "Error: Source and destination directories cannot be sub-directories of each other"
                    };
                }
            }

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Failed; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
            }
        }

        public class InPlaceDefaultProjectTargetPathSetting : Setting
        {
            public override string Name
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

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override IEnumerable<string> NotContainStrings
            {
                get
                {
                    return new[] 
                    {
                        "KuduSync"
                    };
                }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
            }
        }

        public class InPlaceSubFolderProjectSubFolderTargetPathSetting : Setting
        {
            public override string Name
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

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder3"), @"Should have site\repository\subfolder3 folder");
            }
        }

        public class RepositoryDefaultProjectTargetPathSetting : Setting
        {
            public override string Name
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

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
            }
        }

        public class RepositoryDefaultProjectSubFolderTargetPathSetting : Setting
        {
            public override string Name
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

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder"), @"Should have site\repository\subfolder folder");
            }
        }

        public class RepositorySubfolderProjectDefaultTargetPathSetting : Setting
        {
            public override string Name
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

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.False(appManager.VfsManager.Exists(@"site\wwwroot\subfolder2"), @"Should not have site\wwwroot\subfolder2 folder");
            }
        }

        public class RepositorySubfolderProjectSubfolderTargetPathSetting : Setting
        {
            public override string Name
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

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.True(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"site\wwwroot\subfolder3"), @"Should have site\wwwroot\subfolder3 folder");
            }
        }

        public abstract class Scenario
        {
            public abstract string Name { get; }
            public abstract string BuilderTrace { get; }
            public abstract string Content { get; }
            public abstract string DefaultDocument { get; }

            public virtual void GitVerify(GitDeploymentResult result, Setting setting)
            {
                Assert.Contains(BuilderTrace, result.GitTrace);

                foreach (string text in setting.ContainStrings)
                {
                    Assert.Contains(text, result.GitTrace);
                }

                foreach (string text in setting.NotContainStrings)
                {
                    Assert.DoesNotContain(text, result.GitTrace);
                }
            }

            public virtual void VerifyUrl(ApplicationManager appManager, Setting setting)
            {
                if (setting.TargetPath == ".")
                {
                    KuduAssert.VerifyUrl(appManager.SiteUrl + DefaultDocument, Content);
                }
                else
                {
                    VerifyUrl(appManager, setting.TargetPath);
                }
            }

            public virtual void VerifyUrl(ApplicationManager appManager, string documentRoot)
            {
                KuduAssert.VerifyUrl(appManager.SiteUrl + documentRoot + @"/" + DefaultDocument, Content);
            }

            public virtual void ValidateDeploymentStatus(List<DeployResult> results, Setting setting)
            {
                Assert.Equal(1, results.Count);
                Assert.Equal(setting.DeploymentStatus, results[0].Status);
            }
        }

        public class NodeJsAppScenario : Scenario
        {
            public override string Name
            {
                get { return "NodeHelloWorldNoConfig"; }
            }

            public override string Content
            {
                get { return "Hello, world"; }
            }

            public override string BuilderTrace
            {
                get { return "Handling node.js deployment"; }
            }

            public override string DefaultDocument
            {
                get { return null; }
            }
        }

        public class HelloKuduWithSubFolders : Scenario
        {
            public override string Name
            {
                get { return "HelloKuduWithSubFolders"; }
            }

            public override string Content
            {
                get { return "Hello Kudu"; }
            }

            public override string BuilderTrace
            {
                get { return "Handling Basic Web Site deployment"; }
            }

            public override string DefaultDocument
            {
                get { return "index.htm"; }
            }
        }

        // Test: SCM_TARGET_PATH and SCM_REPOSITORY_PATH with absolute path
        public class AbsoluteTargetPathSetting : Setting
        {
            public override string Name
            {
                get
                {
                    return "Scenario : AbsoluteTargetPathSetting, "
                        + "Setting : SCM_REPOSITORY_PATH = " + RepositoryPath
                        + ", PROJECT = " + Project
                        + ", SCM_TARGET_PATH = " + TargetPath;
                }
            }

            public override string RepositoryPath
            {
                get { return @"%HOME%\logFiles\repo"; }
            }

            public override string Project
            {
                get { return "."; }
            }

            public override string TargetPath
            {
                get { return @"%HOME%\logFiles\dest"; }
            }

            public override DeployStatus DeploymentStatus
            {
                get { return DeployStatus.Success; }
            }

            public override IEnumerable<string> ContainStrings
            {
                get
                {
                    return new[] 
                    {
                        @"logFiles\repo",
                        @"logFiles\dest"
                    };
                }
            }

            public override void Verify(ApplicationManager appManager)
            {
                Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");
                Assert.True(appManager.VfsManager.Exists(@"logFiles\repo\.git"), @"Should have logFiles\repo\.git folder");
            }
        }

        public class HelloKuduWithAboluteFolder : Scenario
        {
            public override string Name
            {
                get { return "HelloKuduWithSubFolders"; }
            }

            public override string Content
            {
                get { return "Hello Kudu"; }
            }

            public override string BuilderTrace
            {
                get { return "Handling Basic Web Site deployment"; }
            }

            public override string DefaultDocument
            {
                get { return "index.htm"; }
            }

             public override void VerifyUrl(ApplicationManager appManager, string documentRoot)
            {
                Assert.True(appManager.VfsManager.Exists(@"logFiles\dest\index.htm"), @"Should have logFiles\dest\index.htm file");
                Assert.True(appManager.VfsManager.Exists(@"logFiles\dest\subfolder1\index.htm"), @"Should have logFiles\dest\subfolder1\index.htm file");
                Assert.True(appManager.VfsManager.Exists(@"logFiles\dest\subfolder2\index.htm"), @"Should have logFiles\dest\subfolder2\index.htm file");
            }
        }
    }
}
