using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment
{
    public abstract class SolutionBasedSiteBuilder : ISiteBuilder
    {
        private readonly Executable _msbuildExe;
        private readonly IBuildPropertyProvider _propertyProvider;

        public string SolutionDir
        {
            get
            {
                return Path.GetDirectoryName(SolutionPath);
            }
        }

        public string SolutionPath { get; private set; }

        public SolutionBasedSiteBuilder(IBuildPropertyProvider propertyProvider, string repositoryPath, string solutionPath)
        {
            _propertyProvider = propertyProvider;
            SolutionPath = solutionPath;
            _msbuildExe = new Executable(ResolveMSBuildPath(), repositoryPath);
        }

        protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => p.Key + "=" + p.Value));
        }

        public string ExecuteMSBuild(string arguments, params object[] args)
        {
            return _msbuildExe.Execute(arguments, args);
        }

        public virtual Task Build(DeploymentContext context)
        {
            ILogger innerLogger = context.Logger.Log("Building solution {0}.", Path.GetFileName(SolutionPath));

            try
            {
                string propertyString = GetPropertyString();

                if (!String.IsNullOrEmpty(propertyString))
                {
                    propertyString = " /p:" + propertyString;
                }

                using (context.Profiler.Step("Running msbuild on solution"))
                {
                    // Build the solution first
                    string log = ExecuteMSBuild(@"""{0}"" /verbosity:m /nologo{1}", SolutionPath, propertyString);
                    innerLogger.Log(log);
                }

                return BuildProject(context);
            }
            catch (Exception ex)
            {
                var tcs = new TaskCompletionSource<object>();
                innerLogger.Log("Building solution failed.", LogEntryType.Error);
                innerLogger.Log(ex);
                tcs.SetException(ex);

                return tcs.Task;
            }
        }

        protected abstract Task BuildProject(DeploymentContext context);

        private string ResolveMSBuildPath()
        {
            string windir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Windows);
            return Path.Combine(windir, @"Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
        }
    }
}
