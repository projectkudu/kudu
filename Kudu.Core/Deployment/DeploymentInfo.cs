using Kudu.Core.SourceControl;
using Kudu.Contracts.SourceControl;

namespace Kudu.Core.Deployment
{
    public class DeploymentInfo : DeploymentInfoBase
    {
        private readonly IRepositoryFactory _repositoryFactory;

        public DeploymentInfo(IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public override IRepository GetRepository()
        {
            return _repositoryFactory.EnsureRepository(this.RepositoryType);
        }
    }
}
