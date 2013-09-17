using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class CustomGeneratorCommandSiteBuilder : GeneratorSiteBuilder
    {
        private readonly string _scriptGeneratorArgs;

        public CustomGeneratorCommandSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string scriptGeneratorArgs)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _scriptGeneratorArgs = scriptGeneratorArgs;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                return _scriptGeneratorArgs;
            }
        }

        public override string ProjectType
        {
            get { return "CustomGeneratorCommand"; }
        }
    }
}
