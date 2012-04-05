namespace Kudu.Core.Deployment
{
    public interface IDeploymentCommandGenerator
    {
        string GetDeploymentCommand();
    }
}
