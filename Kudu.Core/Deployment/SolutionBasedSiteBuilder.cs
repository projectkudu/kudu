using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;

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

        public SolutionBasedSiteBuilder(IBuildPropertyProvider propertyProvider, string repositoryPath, string tempPath, string nugetCachePath, string solutionPath)
            : base(propertyProvider, repositoryPath, tempPath, nugetCachePath)
        {
            SolutionPath = solutionPath;
        }
        
        public override Task Build(DeploymentContext context)
        {
            ILogger buildLogger = context.Logger.Log(Resources.Log_BuildingSolution, Path.GetFileName(SolutionPath));

            try
            {
                string propertyString = GetPropertyString();

                if (!String.IsNullOrEmpty(propertyString))
                {
                    propertyString = " /p:" + propertyString;
                }

                using (context.Tracer.Step("Running msbuild on solution"))
                {
                    // Build the solution first
                    string log = ExecuteMSBuild(context.Tracer, @"""{0}"" /verbosity:m /nologo{1}", SolutionPath, propertyString);
                    buildLogger.Log(log);
                }

                return BuildProject(context);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                buildLogger.Log(ex);

                var tcs = new TaskCompletionSource<object>();
                tcs.SetException(ex);

                return tcs.Task;
            }
        }

        protected abstract Task BuildProject(DeploymentContext context);
    }
}
