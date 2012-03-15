using System;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public abstract class MsBuildSiteBuilder : ISiteBuilder
    {
        private const string NuGetCachePathKey = "NuGetCachePath";

        private readonly Executable _msbuildExe;
        private readonly IBuildPropertyProvider _propertyProvider;
        private readonly string _tempPath;

        public MsBuildSiteBuilder(IBuildPropertyProvider propertyProvider, string workingDirectory, string tempPath, string nugetCachePath)
        {
            _propertyProvider = propertyProvider;
            _msbuildExe = new Executable(PathUtility.ResolveMSBuildPath(), workingDirectory);
            _msbuildExe.EnvironmentVariables[NuGetCachePathKey] = nugetCachePath;
            _tempPath = tempPath;
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => p.Key + "=" + p.Value));
        }

        public string ExecuteMSBuild(ITracer tracer, string arguments, params object[] args)
        {
            return _msbuildExe.Execute(tracer, arguments, args).Item1;
        }

        public abstract Task Build(DeploymentContext context);
    }
}
