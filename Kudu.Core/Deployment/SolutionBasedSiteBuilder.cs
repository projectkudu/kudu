using System;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public abstract class SolutionBasedSiteBuilder : MsBuildSiteBuilder
    {
        public string SolutionDir
        {
            get
            {
                return Path.GetDirectoryName(SolutionPath);
            }
        }

        public string SolutionPath { get; private set; }

        public SolutionBasedSiteBuilder(IBuildPropertyProvider propertyProvider, string repositoryPath, string solutionPath, string tempPath)
            : base(propertyProvider, repositoryPath, tempPath)
        {
            SolutionPath = solutionPath;
        }
        
        public override Task Build(DeploymentContext context)
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
                    string log = ExecuteMSBuild(context.Profiler, @"""{0}"" /verbosity:m /nologo{1}", SolutionPath, propertyString);
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
    }
}
