using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public abstract class MsBuildSiteBuilder : ISiteBuilder
    {
        private const string NuGetCachePathKey = "NuGetCachePath";
        private const string NuGetPackageRestoreKey = "EnableNuGetPackageRestore";

        private readonly Executable _msbuildExe;
        private readonly IBuildPropertyProvider _propertyProvider;
        private readonly string _tempPath;

        public MsBuildSiteBuilder(IBuildPropertyProvider propertyProvider, string workingDirectory, string tempPath, string nugetCachePath)
        {
            _propertyProvider = propertyProvider;
            _msbuildExe = new Executable(PathUtility.ResolveMSBuildPath(), workingDirectory);

            // Disable this for now
            // _msbuildExe.EnvironmentVariables[NuGetCachePathKey] = nugetCachePath;

            // NuGet.exe 1.8 will require an environment variable to make package restore work
            _msbuildExe.EnvironmentVariables[NuGetPackageRestoreKey] = "true";

            _tempPath = tempPath;
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => p.Key + "=" + p.Value));
        }

        public string ExecuteMSBuild(ITracer tracer, string arguments, params object[] args)
        {
            var output = new StringBuilder();

            _msbuildExe.Execute(tracer,
            data =>
            {
                output.AppendLine(data);
                Console.WriteLine(data);
            },
            error =>
            {
                Console.Error.WriteLine(error);
            },
            arguments,
            args);

            return output.ToString();
        }

        public abstract Task Build(DeploymentContext context);
    }
}
