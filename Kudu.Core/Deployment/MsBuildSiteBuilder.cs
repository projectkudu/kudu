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
            var consoleWriter = new ConsoleWriter();

            using (var writer = new ProgressWriter(consoleWriter))
            {
                writer.Start();

                // The line with the MSB3644 warnings since it's not important
                return _msbuildExe.Execute(tracer,
                                           output =>
                                           {
                                               if (output.Contains("MSB3644:"))
                                               {
                                                   return false;
                                               }

                                               consoleWriter.WriteOutLine(output);
                                               return true;
                                           },
                                           error =>
                                           {
                                               consoleWriter.WriteErrorLine(error);
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
