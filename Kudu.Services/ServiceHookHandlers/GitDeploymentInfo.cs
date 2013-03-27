
namespace Kudu.Services.ServiceHookHandlers
{
    public class GitDeploymentInfo : DeploymentInfo
    {
        public GitDeploymentInfo()
        {
            IsContinuous = true;
        }

        public string NewRef { get; set; }
    }
}
