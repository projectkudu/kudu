using System;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Deployment.Generator;

namespace Kudu.Core.Deployment
{
    public abstract class MsBuildSiteBuilder : ISiteBuilder
    {
        private readonly Executable _msbuildExe;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IBuildPropertyProvider _propertyProvider;
        private readonly string _tempPath;

        public MsBuildSiteBuilder(IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string workingDirectory, string tempPath)
        {
            _settings = settings;
            _propertyProvider = propertyProvider;
            _msbuildExe = new Executable(PathUtility.ResolveMSBuildPath(), workingDirectory, settings.GetCommandIdleTimeout());

            // NuGet.exe 1.8 will require an environment variable to make package restore work
            _msbuildExe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

            _tempPath = tempPath;
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => String.Format("{0}=\"{1}\"", p.Key, p.Value)));
        }

        protected string GetMSBuildExtraArguments()
        {
            if (_settings == null) return String.Empty;

            return _settings.GetValue(SettingsKeys.BuildArgs);
        }

        public virtual string ExecuteMSBuild(ITracer tracer, string arguments, params object[] args)
        {
            return _msbuildExe.ExecuteWithProgressWriter(tracer, ExternalCommandBuilder.ShouldFilterOutMsBuildWarnings, _ => false, arguments, args).Item1;
        }

        public abstract Task Build(DeploymentContext context);
    }
}
