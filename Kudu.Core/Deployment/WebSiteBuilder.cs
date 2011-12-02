using System;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class WebSiteBuilder : SolutionBasedSiteBuilder
    {
        private readonly string _projectPath;

        public WebSiteBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string solutionPath, string projectPath)
            : base(propertyProvider, sourcePath, solutionPath)
        {
            _projectPath = projectPath;
        }

        protected override Task BuildProject(string outputPath, ILogger logger)
        {
            var tcs = new TaskCompletionSource<object>();

            var innerLogger = logger.Log("Using website project {0}.", _projectPath);

            try
            {
                FileSystemHelpers.SmartCopy(_projectPath, outputPath);

                innerLogger.Log("Done.");
            }
            catch (Exception e)
            {
                innerLogger.Log("Copying website failed.", LogEntryType.Error);
                innerLogger.Log(e);
                tcs.TrySetException(e);
                return tcs.Task;
            }

            tcs.SetResult(null);

            return tcs.Task;
        }
    }
}
