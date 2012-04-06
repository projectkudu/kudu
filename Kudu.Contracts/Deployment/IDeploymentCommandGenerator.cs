namespace Kudu.Core.Deployment
{
    public interface IDeploymentCommandGenerator
    {
        string DeploymentEnvironmentVariable { get; }
        string GetDeploymentExePath();
        string GetDeploymentCommand();
    }
}
