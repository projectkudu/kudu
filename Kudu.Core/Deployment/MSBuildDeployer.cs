using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Deployment {
    public class MSBuildDeployer : IDeployer {
        private readonly IEnvironment _environment;
        private readonly Executable _msbuildExe;

        public MSBuildDeployer(IEnvironment environment) {
            _environment = environment;
            _msbuildExe = new Executable(ResolveMSBuildPath(), environment.RepositoryPath);
        }

        public void Deploy(string id) {
            // TODO: Have some convention (or setting to determine the deployment project if more than one)
            string projectFile = _environment.GetWebApplicationProjects().FirstOrDefault();

            // REVIEW: Should we use the msbuild API?
            _msbuildExe.Execute("{0} /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir={1};AutoParameterizationWebConfigConnectionStrings=false", projectFile, _environment.DeploymentPath);
        }

        private string ResolveMSBuildPath() {
            string windir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Windows);
            return Path.Combine(windir, @"Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
        }
    }
}
