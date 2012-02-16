using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment
{
    public abstract class MsBuildSiteBuilder : ISiteBuilder
    {
        private readonly Executable _msbuildExe;
        private readonly IBuildPropertyProvider _propertyProvider;

        public MsBuildSiteBuilder(IBuildPropertyProvider propertyProvider, string workingDirectory)
        {
            _propertyProvider = propertyProvider;
            _msbuildExe = new Executable(ResolveMSBuildPath(), workingDirectory);
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => p.Key + "=" + p.Value));
        }

        public string ExecuteMSBuild(ILogger logger, string arguments, params object[] args)
        {
#if DEBUG
            logger.Log(String.Format(arguments, args));
#endif
            return _msbuildExe.Execute(arguments, args).Item1;
        }

        public abstract Task Build(DeploymentContext context);
            
        private string ResolveMSBuildPath()
        {
            string windir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Windows);
            return Path.Combine(windir, @"Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
        }
    }
}
