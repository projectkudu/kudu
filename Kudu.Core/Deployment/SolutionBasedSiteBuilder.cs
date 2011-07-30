using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment {
    public abstract class SolutionBasedSiteBuilder : ISiteBuilder {
        private readonly Executable _msbuildExe;
        
        public string SolutionDir {
            get {
                return Path.GetDirectoryName(SolutionPath);
            }
        }

        public string SolutionPath { get; set; }

        public SolutionBasedSiteBuilder(string repositoryPath, string solutionPath) {
            SolutionPath = solutionPath;
            _msbuildExe = new Executable(ResolveMSBuildPath(), repositoryPath);
        }

        public string ExecuteMSBuild(string arguments, params object[] args) {
            return _msbuildExe.Execute(arguments, args);
        }

        public Task Build(string outputPath, ILogger logger) {
            var tcs = new TaskCompletionSource<object>();

            try {
                logger.Log("Building solution {0}.", Path.GetFileName(SolutionPath));

                // Build the solution first
                string log = ExecuteMSBuild(@"""{0}"" /verbosity:m /nologo", SolutionPath);

                logger.Log(log);
            }
            catch (Exception ex) {
                logger.Log("Building solution failed.", LogEntryType.Error);
                logger.Log(ex);
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return BuildProject(outputPath, logger);
        }

        protected abstract Task BuildProject(string outputPath, ILogger logger);

        private string ResolveMSBuildPath() {
            string windir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Windows);
            return Path.Combine(windir, @"Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
        }
    }
}
