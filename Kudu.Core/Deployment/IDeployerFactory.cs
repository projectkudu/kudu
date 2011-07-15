namespace Kudu.Core.Deployment {
    public interface IDeployerFactory {
        IDeployer CreateDeployer();
    }
}
