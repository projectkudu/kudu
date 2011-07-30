using System;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
    public class WebSiteBuilder : SolutionBasedSiteBuilder {
        private readonly string _projectPath;

        public WebSiteBuilder(string sourcePath, string solutionPath, string projectPath)
            : base(sourcePath, solutionPath) {
            _projectPath = projectPath;
        }

        protected override Task BuildProject(string outputPath, ILogger logger) {
            var tcs = new TaskCompletionSource<object>();

            try {
                logger.Log("Using website project {0}.", _projectPath);

                FileSystemHelpers.SmartCopy(_projectPath, outputPath);

                logger.Log("Done.");
            }
            catch (Exception e) {
                logger.Log("Copying website failed.", LogEntryType.Error);
                logger.Log(e);
                tcs.TrySetException(e);
                return tcs.Task;
            }

            tcs.SetResult(null);

            return tcs.Task;
        }
    }
}
