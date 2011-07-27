using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment {
    public class MSBuildDeployer : IDeployer {
        private readonly string _solutionPath;
        private readonly string _projectPath;
        private readonly Executable _msbuildExe;

        public MSBuildDeployer(string sourcePath, string solutionPath, string projectPath) {
            _solutionPath = solutionPath;
            _projectPath = projectPath;
            _msbuildExe = new Executable(ResolveMSBuildPath(), sourcePath);
        }

        public Task Deploy(string targetPath, ILogger logger) {
            var tcs = new TaskCompletionSource<object>();

            // Locate the solution directory
            string solutionDir = Path.GetDirectoryName(_solutionPath) + "\\";

            try {
                // Build the solution first
                _msbuildExe.Execute(@"""{0}""", _solutionPath);
            }
            catch (Exception e) {
                logger.WriteLog(e.Message);
                tcs.TrySetException(e);
                return tcs.Task;
            }

            try {
                // REVIEW: Should we use the msbuild API?
                string log = _msbuildExe.Execute(@"""{0}"" /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir={1};AutoParameterizationWebConfigConnectionStrings=false;SolutionDir={2}", _projectPath, targetPath, solutionDir);

                logger.WriteLog(log);
            }
            catch (Exception e) {
                logger.WriteLog(e.Message);
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
