using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment
{
    public static class DeploymentManagerExtensions
    {
        public static void Deploy(this IDeploymentManager deploymentManager, IRepository repository, string id, string deployer, bool clean)
        {
            ChangeSet changeSet = repository.GetChangeSet(id);
            deploymentManager.Deploy(repository, changeSet, deployer, clean);
        }
    }
}
