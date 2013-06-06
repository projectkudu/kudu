using System.Threading.Tasks;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Deployment
{
    public static class DeploymentManagerExtensions
    {
        public static Task DeployAsync(this IDeploymentManager deploymentManager, IRepository repository, string id, string deployer, bool clean)
        {
            ChangeSet changeSet = null;
            if (id != null)
            {
                changeSet = repository.GetChangeSet(id);
            }

            return deploymentManager.DeployAsync(repository, changeSet, deployer, clean);
        }
    }
}
