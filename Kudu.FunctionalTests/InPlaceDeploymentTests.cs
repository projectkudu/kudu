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
        [PropertyData("Scenarios")]
        public async Task InPlaceDeploymentBasicTests(IScenario scenario)
        {
            var appName = KuduUtils.GetRandomWebsiteName(scenario.Name);
            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                await appManager.SettingsManager.SetValue(SettingsKeys.RepositoryPath, "wwwroot");

                using (TestRepository testRepository = Git.Clone(scenario.Name, scenario.CloneUrl))
                {
                    var result = appManager.GitDeploy(testRepository.PhysicalPath);

                    // Inplace should not do KuduSync
                    Assert.DoesNotContain("KuduSync", result.GitTrace);

                    // Repository path should not exist
                    Assert.False(appManager.VfsManager.Exists(@"site\repository\.git"), @"Should not have site\repository\.git folder");

                    // Validate builder
                    Assert.Contains(scenario.BuilderTrace, result.GitTrace);
                }

                // Validate deployment status
                var results = (await appManager.DeploymentManager.GetResultsAsync()).ToList();
                Assert.Equal(1, results.Count);
                Assert.Equal(DeployStatus.Success, results[0].Status);

                // Validate site
                scenario.Verify(appManager);
            });
        }

        public static IEnumerable<object[]> Scenarios
        {
            get
            {
                yield return new object[] { new NodeJsAppScenario() };
                yield return new object[] { new HelloKuduScenario() };
            }
        }

        public interface IScenario
        {
            string Name { get; }
            string CloneUrl { get; }
            string BuilderTrace { get; }
            void Verify(ApplicationManager appManager);
        }

        public class HelloKuduScenario : IScenario
        {
            public string Name
            {
                get { return "HelloKudu"; }
            }

            public string CloneUrl
            {
                get { return "https://github.com/KuduApps/HelloKudu.git"; }
            }

            public string BuilderTrace
            {
                get { return "Handling Basic Web Site deployment"; }
            }

            public void Verify(ApplicationManager appManager)
            {
                KuduAssert.VerifyUrl(appManager.SiteUrl + "index.htm", "Hello Kudu");
            }
        }

        public class NodeJsAppScenario : IScenario
        {
            public string Name
            {
                get { return "NodeHelloWorldNoConfig"; }
            }

            public string CloneUrl
            {
                get { return "https://github.com/KuduApps/NodeHelloWorldNoConfig.git"; }
            }

            public string BuilderTrace
            {
                get { return "Handling node.js deployment"; }
            }

            public void Verify(ApplicationManager appManager)
            {
                KuduAssert.VerifyUrl(appManager.SiteUrl, "Hello, world");
            }
        }
    }
}