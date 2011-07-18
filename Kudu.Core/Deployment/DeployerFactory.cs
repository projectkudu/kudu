namespace Kudu.Core.Deployment {
    public class DeployerFactory : IDeployerFactory {
        private readonly IEnvironment _environment;
        public DeployerFactory(IEnvironment environment) {
            _environment = environment;
        }

        public IDeployer CreateDeployer() {
            if (_environment.RequiresBuild) {
                return new MSBuildDeployer(_environment);
            }

            return new BasicDeployer(_environment);
        }
    }
}
