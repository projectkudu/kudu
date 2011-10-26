using System;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class WapBuilder : SolutionBasedSiteBuilder
    {
        private readonly string _projectPath;

        public WapBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string solutionPath, string projectPath)
            : base(propertyProvider, sourcePath, solutionPath)
        {
            _projectPath = projectPath;
        }

        protected override Task BuildProject(string outputPath, ILogger logger)
        {
            var tcs = new TaskCompletionSource<object>();

            string solutionDir = SolutionDir + @"\";

            try
            {
                logger.Log("Building web project {0}.", Path.GetFileName(_projectPath));

                string log = ExecuteMSBuild(@"""{0}"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir={1};AutoParameterizationWebConfigConnectionStrings=false;SolutionDir={2};{3}", _projectPath, outputPath, solutionDir, GetPropertyString());

                logger.Log(log);
            }
            catch (Exception ex)
            {
                logger.Log("Building web project failed.", LogEntryType.Error);
                logger.Log(ex);
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            tcs.SetResult(null);

            return tcs.Task;
        }
    }
}
