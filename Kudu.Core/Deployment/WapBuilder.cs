using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment {
    public class WapBuilder : ISiteBuilder {
        private readonly string _solutionPath;
        private readonly string _projectPath;
        private readonly Executable _msbuildExe;

        public WapBuilder(string sourcePath, string solutionPath, string projectPath) {
            _solutionPath = solutionPath;
            _projectPath = projectPath;
            _msbuildExe = new Executable(ResolveMSBuildPath(), sourcePath);
        }

        public Task Build(string outputPath, ILogger logger) {
            var tcs = new TaskCompletionSource<object>();

            // Locate the solution directory
            string solutionDir = Path.GetDirectoryName(_solutionPath) + "\\";

            try {
                logger.Log("Builing solution {0}.", Path.GetFileName(_solutionPath));

                // Build the solution first
                string log = _msbuildExe.Execute(@"""{0}"" /verbosity:m /nologo", _solutionPath);

                logger.Log(log);
            }
            catch (Exception e) {
                logger.Log("Building solution failed.", LogEntryType.Error);
                logger.Log(e);
                tcs.TrySetException(e);
                return tcs.Task;
            }

            try {
                logger.Log("Building web project {0}.", Path.GetFileName(_projectPath));

                // REVIEW: Should we use the msbuild API?
                string log = _msbuildExe.Execute(@"""{0}"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir={1};AutoParameterizationWebConfigConnectionStrings=false;SolutionDir={2}", _projectPath, outputPath, solutionDir);

                logger.Log(log);
            }
            catch (Exception e) {
                logger.Log("Building web project failed.", LogEntryType.Error);
                logger.Log(e);
                tcs.TrySetException(e);
                return tcs.Task;
            }

            tcs.SetResult(null);

            return tcs.Task;
        }

        private string ResolveMSBuildPath() {
            string windir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Windows);
            return Path.Combine(windir, @"Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
        }
    }
}
