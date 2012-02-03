namespace Kudu.Core.Deployment
{
    public interface IDeploymentManagerFactory
    {
        IDeploymentManager CreateDeploymentManager();
    }
}
