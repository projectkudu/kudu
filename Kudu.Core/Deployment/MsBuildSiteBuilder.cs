using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment
{
    public abstract class MsBuildSiteBuilder : ISiteBuilder
    {
        private readonly Executable _msbuildExe;
        private readonly IBuildPropertyProvider _propertyProvider;
        private readonly string _tempPath;

        public MsBuildSiteBuilder(IBuildPropertyProvider propertyProvider, string workingDirectory, string tempPath)
        {
            _propertyProvider = propertyProvider;
            _msbuildExe = new Executable(PathUtility.ResolveMSBuildPath(), workingDirectory);
            _tempPath = tempPath;
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => p.Key + "=" + p.Value));
        }

        public string ExecuteMSBuild(IProfiler profiler, string arguments, params object[] args)
        {
            return _msbuildExe.Execute(profiler, arguments, args).Item1;
        }

        public abstract Task Build(DeploymentContext context);
    }
}
