using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.SourceControl;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kudu.Core.Test.Deployment.Generator
{
    public class SiteBuilderFactoryFacts
    {
        [Theory]
        [MemberData("DeploymentSettingsScenarios")]
        public void EnsureCorrectPriorityOfOverridesWhenCreatingBuilder(
            IDeploymentSettingsManager settings,
            IRepository repository,
            Type expectedBuilderType)
        {
            var environment = Mock.Of<IEnvironment>();
            var pProvider = Mock.Of<IBuildPropertyProvider>();
            var tracer = Mock.Of<ITracer>();
            var logger = Mock.Of<ILogger>();

            var factory = new SiteBuilderFactory(pProvider, environment);

            var result = factory.CreateBuilder(tracer, logger, settings, repository);

            Assert.IsType(expectedBuilderType, result);
        }

        public static IEnumerable<object[]> DeploymentSettingsScenarios
        {
            get
            {
                var settings = new Dictionary<string, string>
                {
                    { SettingsKeys.Command, "CustomCommand" },
                    { SettingsKeys.ScriptGeneratorArgs, "ScriptGeneratorArgs" },
                    { SettingsKeys.DoBuildDuringDeployment, "false" }
                };

                var repo = Mock.Of<IRepository>();

                yield return new object[] { GetSettings(settings), repo, typeof(CustomBuilder) };

                settings.Remove(SettingsKeys.Command);
                yield return new object[] { GetSettings(settings), repo, typeof(CustomGeneratorCommandSiteBuilder) };

                settings.Remove(SettingsKeys.ScriptGeneratorArgs);
                yield return new object[] { GetSettings(settings), repo, typeof(BasicBuilder) };
            }
        }

        private static IDeploymentSettingsManager GetSettings(IDictionary<string, string> settings)
        {
            var m = new Mock<IDeploymentSettingsManager>();
            foreach (var item in settings)
            {
                m.Setup(d => d.GetValue(item.Key, false)).Returns(item.Value);
            }

            return m.Object;
        }
    }
}
