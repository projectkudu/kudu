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

            // Disable this for now
            // _msbuildExe.EnvironmentVariables[NuGetCachePathKey] = nugetCachePath;

            // NuGet.exe 1.8 will require an environment variable to make package restore work
            _msbuildExe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

            _tempPath = tempPath;
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => String.Format("{0}=\"{1}\"", p.Key, p.Value)));
        }

        public virtual string ExecuteMSBuild(ITracer tracer, string arguments, params object[] args)
        {
            using (var writer = new ProgressWriter())
            {
                writer.Start();

                // The line with the MSB3644 warnings since it's not important
                return _msbuildExe.Execute(tracer,
                                           output =>
                                           {
                                               if (output.Contains("MSB3644:") || output.Contains("MSB3270:"))
                                               {
                                                   return false;
                                               }

                                               writer.WriteOutLine(output);
                                               return true;
                                           },
                                           error =>
                                           {
                                               writer.WriteErrorLine(error);
                                               return true;
                                           },
                                           Console.OutputEncoding,
                                           arguments,
                                           args).Item1;
            }
        }

        public abstract Task Build(DeploymentContext context);
    }
}
